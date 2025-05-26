// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> CreateRollbackPackageAsync(IReadOnlyCollection<string> sourcePackagePaths, string rollbackPackagePath, CosmosAuthInfo cosmosAuthInfo, CancellationToken cancellationToken)
    {
        Debug.Assert(sourcePackagePaths is not null);
        Debug.Assert(rollbackPackagePath is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        if (sourcePackagePaths.Count == 0)
        {
            return true;
        }

        using var packageVersion = new VersionBuilder();
        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo);

        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();
        var deployOperations = new HashSet<(PackageDocumentKey, PackageOperationType)>();
        var deployDocumentStates = new Dictionary<PackageDocumentKey, (Dictionary<PackageOperationType, JsonObject> Sources, JsonObject? Target)>();

        foreach (var packagePath in sourcePackagePaths)
        {
            _logger.LogInformation("{SourcePath} + {CosmosEndpoint} >>> {TargetPath}", packagePath, cosmosClient.Endpoint, rollbackPackagePath);

            using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            packageVersion.Append(package.PackageProperties.Identifier);

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

                                CosmosDocument.Prune(sourceDocument);

                                if (!CosmosDocument.TryGetId(sourceDocument, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Failed to extract document partition key from {packagePartitionUri}:$[{i}]");
                                }

                                var documentKey = new PackageDocumentKey(
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentId,
                                    documentPartitionKey);

                                if (!deployOperations.Add((documentKey, packagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"A duplicate document+operation entry {packagePartitionUri}:$[{i}]");
                                }

                                if (!deployDocumentStates.TryGetValue(documentKey, out var deployDocumentState))
                                {
                                    var targetDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            documentId,
                                            documentPartitionKey,
                                            (int)operationResponse.StatusCode,
                                            Math.Round(operationResponse.RequestCharge, 2));

                                        targetDocument = operationResponse.Resource;
                                        packageVersion.Append(operationResponse.ETag);
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if ((int)ex.StatusCode == 404)
                                        {
                                            _logger.LogInformation(
                                                "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
                                                packagePartition.DatabaseName,
                                                packagePartition.ContainerName,
                                                documentId,
                                                documentPartitionKey,
                                                (int)ex.StatusCode,
                                                Math.Round(ex.RequestCharge, 2));
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
                    var containerPartitionKeyPathsKey = (operationGroupByDatabase.Key, operationGroupByContainer.Key);
                    var containerPartitionKeyPaths = partitionKeyPathsCache[containerPartitionKeyPathsKey];

                    var operationGroupsByOperation = operationGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var operationGroupByOperation in operationGroupsByOperation)
                    {
                        var packagePartition = new PackagePartition(
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
                            var document = documents[i];

                            CosmosDocument.TryGetId(document, out var documentId);
                            CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths!, out var documentPartitionKey);

                            _logger.LogInformation(
                                "+++ {OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey} ({PropertyCount})",
                                packagePartitionOperationName,
                                packagePartition.DatabaseName,
                                packagePartition.ContainerName,
                                documentId,
                                documentPartitionKey,
                                document.Count);
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
            package.PackageProperties.Version = packageVersion.ToVersion();
            package.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            package.PackageProperties.Created = _timeProvider.GetUtcNow().UtcDateTime;
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
