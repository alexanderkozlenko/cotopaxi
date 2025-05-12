// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Globalization;
using System.IO.Packaging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Primitives;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> CreateCheckpointPackagesAsync(IReadOnlyCollection<string> sourcePackagePaths, string rollbackPackagePath, CosmosAuthInfo cosmosAuthInfo, CancellationToken cancellationToken)
    {
        Debug.Assert(sourcePackagePaths is not null);
        Debug.Assert(rollbackPackagePath is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        if (sourcePackagePaths.Count == 0)
        {
            return true;
        }

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
        };

        using var cosmosClient = cosmosAuthInfo.IsConnectionString ?
            new CosmosClient(cosmosAuthInfo.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosAuthInfo.AccountEndpoint.AbsoluteUri, cosmosAuthInfo.AuthKeyOrResourceToken, cosmosClientOptions);

        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();
        var deployOperations = new HashSet<(PackageDocumentKey, PackageOperationType)>();
        var deployDocumentStates = new Dictionary<PackageDocumentKey, (Dictionary<PackageOperationType, JsonObject> Sources, JsonObject? Target)>();

        _logger.LogInformation("Generating rollback package {TargetPath} for endpoint {CosmosEndpoint}", rollbackPackagePath, cosmosClient.Endpoint);

        foreach (var packagePath in sourcePackagePaths)
        {
            _logger.LogInformation("Analyzing source package {SourcePath}", packagePath);

            using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var packagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

            using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
            {
                packagePartitions = packageModel.GetPartitions();
            }

            var packagePartitionGroupsByDatabase = packagePartitions
                .GroupBy(static x => x.Value.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var packagePartitionGroupByDatabase in packagePartitionGroupsByDatabase)
            {
                var packagePartitionGroupsByContainer = packagePartitionGroupByDatabase
                    .GroupBy(static x => x.Value.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var packagePartitionGroupByContainer in packagePartitionGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(packagePartitionGroupByDatabase.Key, packagePartitionGroupByContainer.Key);
                    var containerPartitionKeyPathsKey = (packagePartitionGroupByDatabase.Key, packagePartitionGroupByContainer.Key);

                    if (!partitionKeyPathsCache.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                    {
                        var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                        partitionKeyPathsCache.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);
                    }

                    var packagePartitionGroupsByOperation = packagePartitionGroupByContainer
                        .GroupBy(static x => x.Value.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var packagePartitionGroupByOperation in packagePartitionGroupsByOperation)
                    {
                        var packagePartitionsByOperation = packagePartitionGroupByOperation
                            .OrderBy(static x => x.Key.OriginalString, StringComparer.Ordinal);

                        foreach (var (packagePartitionUri, packagePartition) in packagePartitionsByOperation)
                        {
                            var packagePartitionOperationName = packagePartition.OperationType.ToString().ToLowerInvariant();
                            var packagePart = package.GetPart(packagePartitionUri);
                            var documents = default(JsonObject?[]);

                            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            deployOperations.EnsureCapacity(deployOperations.Count + documents.Length);

                            for (var i = 0; i < documents.Length; i++)
                            {
                                var sourceDocument = documents[i];

                                if (sourceDocument is null)
                                {
                                    continue;
                                }

                                _logger.LogInformation(
                                    "Analyzing cdbpkg:{PartitionKey}:$[{DocumentIndex}] for {OperationName} in {DatabaseName}\\{ContainerName}",
                                    packagePartition.PartitionKey,
                                    i,
                                    packagePartitionOperationName,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName);

                                CosmosDocument.Prune(sourceDocument);

                                if (!CosmosDocument.TryGetId(sourceDocument, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Failed to extract document partition key from cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                var documentKey = new PackageDocumentKey(
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentId,
                                    documentPartitionKey);

                                if (!deployOperations.Add((documentKey, packagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"A duplicate document+operation entry cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                if (!deployDocumentStates.TryGetValue(documentKey, out var deployDocumentState))
                                {
                                    var targetDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "Requesting document for cdbpkg:{PartitionKey}:$[{DocumentIndex}] from {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                            packagePartition.PartitionKey,
                                            i,
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            (int)operationResponse.StatusCode);

                                        targetDocument = operationResponse.Resource;
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (ex.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            _logger.LogInformation(
                                                "Requesting document for cdbpkg:{PartitionKey}:$[{DocumentIndex}] from {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                                packagePartition.PartitionKey,
                                                i,
                                                packagePartition.DatabaseName,
                                                packagePartition.ContainerName,
                                                (int)ex.StatusCode);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }

                                    if (targetDocument is not null)
                                    {
                                        CosmosDocument.Prune(targetDocument);
                                    }

                                    deployDocumentState = (new(), targetDocument);
                                    deployDocumentStates.Add(documentKey, deployDocumentState);
                                }

                                deployDocumentState.Sources.Add(packagePartition.OperationType, sourceDocument);
                            }
                        }
                    }
                }
            }
        }

        var rollbackOperations = new List<(string DatabaseName, string ContainerName, JsonObject Document, PackageOperationType OperationType)>();

        foreach (var (documentKey, documentState) in deployDocumentStates)
        {
            var sourceDocument = default(JsonObject);
            var targetDocument = documentState.Target;

            if (targetDocument is null)
            {
                if (documentState.Sources.TryGetValue(PackageOperationType.Create, out sourceDocument) ||
                    documentState.Sources.TryGetValue(PackageOperationType.Upsert, out sourceDocument))
                {
                    var rollbackOperation = (
                        documentKey.DatabaseName,
                        documentKey.ContainerName,
                        sourceDocument,
                        PackageOperationType.Delete);

                    rollbackOperations.Add(rollbackOperation);
                }
            }
            else
            {
                if (documentState.Sources.ContainsKey(PackageOperationType.Delete))
                {
                    var rollbackOperation = (
                        documentKey.DatabaseName,
                        documentKey.ContainerName,
                        targetDocument,
                        PackageOperationType.Upsert);

                    rollbackOperations.Add(rollbackOperation);

                    continue;
                }

                if (documentState.Sources.TryGetValue(PackageOperationType.Upsert, out sourceDocument))
                {
                    var rollbackRequired = documentState.Sources.ContainsKey(PackageOperationType.Patch) || !JsonNode.DeepEquals(sourceDocument, targetDocument);

                    if (rollbackRequired)
                    {
                        var rollbackOperation = (
                            documentKey.DatabaseName,
                            documentKey.ContainerName,
                            targetDocument,
                            PackageOperationType.Upsert);

                        rollbackOperations.Add(rollbackOperation);
                    }

                    continue;
                }

                if (documentState.Sources.TryGetValue(PackageOperationType.Patch, out sourceDocument))
                {
                    var rollbackRequired = sourceDocument.Any(x => !targetDocument.TryGetPropertyValue(x.Key, out var v) || !JsonNode.DeepEquals(x.Value, v));

                    if (rollbackRequired)
                    {
                        var rollbackOperation = (
                            documentKey.DatabaseName,
                            documentKey.ContainerName,
                            targetDocument,
                            PackageOperationType.Upsert);

                        rollbackOperations.Add(rollbackOperation);
                    }
                }
            }
        }

        var rollbackPackageDirectory = Path.GetDirectoryName(rollbackPackagePath);

        if (rollbackPackageDirectory is not null)
        {
            Directory.CreateDirectory(rollbackPackageDirectory);
        }

        try
        {
            using var package = Package.Open(rollbackPackagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

            var operationGroupsByDatabase = rollbackOperations
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var operationGroupByDatabase in operationGroupsByDatabase)
            {
                var operationGroupsByContainer = operationGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var operationGroupByContainer in operationGroupsByContainer)
                {
                    var operationGroupsByOperation = operationGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var operationGroupByOperation in operationGroupsByOperation)
                    {
                        var packagePartitionKeySource = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}:{1}:{2}",
                            operationGroupByDatabase.Key,
                            operationGroupByContainer.Key,
                            operationGroupByOperation.Key);

                        var packagePartitionKey = Uuid.CreateVersion8(packagePartitionKeySource);

                        var packagePartition = new PackagePartition(
                            packagePartitionKey,
                            operationGroupByDatabase.Key,
                            operationGroupByContainer.Key,
                            operationGroupByOperation.Key);

                        var packagePartitionOperationName = packagePartition.OperationType.ToString().ToLowerInvariant();
                        var packagePartitionUri = packageModel.CreatePartition(packagePartition);

                        var documents = operationGroupByOperation
                            .Select(static x => x.Document)
                            .ToArray();

                        for (var i = 0; i < documents.Length; i++)
                        {
                            _logger.LogInformation(
                                "Packing cdbpkg:{PartitionKey}:$[{DocumentIndex}] for {OperationName} in {DatabaseName}\\{ContainerName}",
                                packagePartitionKey,
                                i,
                                packagePartitionOperationName,
                                packagePartition.DatabaseName,
                                packagePartition.ContainerName);
                        }

                        var packagePart = package.CreatePart(packagePartitionUri, "application/json", default);

                        using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(packagePartStream, documents, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await packageModel.SaveAsync(cancellationToken).ConfigureAwait(false);

            package.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            package.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            package.PackageProperties.Created = DateTime.UtcNow;
            package.PackageProperties.Creator = s_applicationName;
        }
        catch (Exception ex)
        {
            try
            {
                File.Delete(rollbackPackagePath);
            }
            catch (Exception exio)
            {
                throw new AggregateException(ex, exio);
            }

            throw;
        }

        return true;
    }
}
