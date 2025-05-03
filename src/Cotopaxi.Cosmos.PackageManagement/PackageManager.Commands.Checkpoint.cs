// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        foreach (var sourcePackagePath in sourcePackagePaths)
        {
            _logger.LogInformation("Analyzing source package {SourcePath}", sourcePackagePath);

            using var sourcePackage = Package.Open(sourcePackagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var sourcePackagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

            using (var sourcePackageModel = await PackageModel.OpenAsync(sourcePackage, default, cancellationToken).ConfigureAwait(false))
            {
                sourcePackagePartitions = sourcePackageModel.GetPartitions();
            }

            var sourcePackagePartitionGroupsByDatabase = sourcePackagePartitions
                .GroupBy(static x => x.Value.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var sourcePackagePartitionGroupByDatabase in sourcePackagePartitionGroupsByDatabase)
            {
                var sourcePackagePartitionGroupsByContainer = sourcePackagePartitionGroupByDatabase
                    .GroupBy(static x => x.Value.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var sourcePackagePartitionGroupByContainer in sourcePackagePartitionGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);
                    var containerPartitionKeyPathsKey = (sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);

                    if (!partitionKeyPathsCache.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                    {
                        var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                        partitionKeyPathsCache.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);
                    }

                    var sourcePackagePartitionGroupsByOperation = sourcePackagePartitionGroupByContainer
                        .GroupBy(static x => x.Value.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var sourcePackagePartitionGroupByOperation in sourcePackagePartitionGroupsByOperation)
                    {
                        var sourcePackagePartitionsByOperation = sourcePackagePartitionGroupByOperation
                            .OrderBy(static x => x.Key.OriginalString, StringComparer.Ordinal);

                        foreach (var (sourcePackagePartitionUri, sourcePackagePartition) in sourcePackagePartitionsByOperation)
                        {
                            var sourcePackagePartitionOperationName = sourcePackagePartition.OperationType.ToString().ToLowerInvariant();
                            var sourcePackagePart = sourcePackage.GetPart(sourcePackagePartitionUri);
                            var sourceDocuments = default(JsonObject?[]);

                            using (var sourcePackagePartStream = sourcePackagePart.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                sourceDocuments = await JsonSerializer.DeserializeAsync<JsonObject?[]>(sourcePackagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            deployOperations.EnsureCapacity(deployOperations.Count + sourceDocuments.Length);

                            for (var i = 0; i < sourceDocuments.Length; i++)
                            {
                                var sourceDocument = sourceDocuments[i];

                                if (sourceDocument is null)
                                {
                                    continue;
                                }

                                _logger.LogInformation(
                                    "Analyzing cdbpkg:{PartitionKey}:$[{DocumentIndex}] for {OperationName} in {DatabaseName}\\{ContainerName}",
                                    sourcePackagePartition.PartitionKey,
                                    i,
                                    sourcePackagePartitionOperationName,
                                    sourcePackagePartition.DatabaseName,
                                    sourcePackagePartition.ContainerName);

                                CosmosDocument.Prune(sourceDocument);

                                if (!CosmosDocument.TryGetId(sourceDocument, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Failed to extract document partition key from cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                var documentKey = new PackageDocumentKey(
                                    sourcePackagePartition.DatabaseName,
                                    sourcePackagePartition.ContainerName,
                                    documentId,
                                    documentPartitionKey);

                                if (!deployOperations.Add((documentKey, sourcePackagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"A duplicate document+operation entry cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                if (!deployDocumentStates.TryGetValue(documentKey, out var deployDocumentState))
                                {
                                    var targetDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "Requesting document for cdbpkg:{PartitionKey}:$[{DocumentIndex}] from {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                            sourcePackagePartition.PartitionKey,
                                            i,
                                            sourcePackagePartition.DatabaseName,
                                            sourcePackagePartition.ContainerName,
                                            (int)operationResponse.StatusCode);

                                        targetDocument = operationResponse.Resource;
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (ex.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            _logger.LogInformation(
                                                "Requesting document for cdbpkg:{PartitionKey}:$[{DocumentIndex}] from {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                                sourcePackagePartition.PartitionKey,
                                                i,
                                                sourcePackagePartition.DatabaseName,
                                                sourcePackagePartition.ContainerName,
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

                                deployDocumentState.Sources.Add(sourcePackagePartition.OperationType, sourceDocument);
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

        Directory.CreateDirectory(Path.GetDirectoryName(rollbackPackagePath)!);

        try
        {
            using var rollbackPackage = Package.Open(rollbackPackagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var rollbackPackageModel = await PackageModel.OpenAsync(rollbackPackage, default, cancellationToken).ConfigureAwait(false);

            var rollbackOperationGroupsByDatabase = rollbackOperations
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var rollbackOperationGroupByDatabase in rollbackOperationGroupsByDatabase)
            {
                var rollbackOperationGroupsByContainer = rollbackOperationGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var rollbackOperationGroupByContainer in rollbackOperationGroupsByContainer)
                {
                    var rollbackOperationGroupsByOperation = rollbackOperationGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var rollbackOperationGroupByOperation in rollbackOperationGroupsByOperation)
                    {
                        var packagePartitionKey = Guid.CreateVersion7();

                        var rollbackPackagePartition = new PackagePartition(
                            packagePartitionKey,
                            rollbackOperationGroupByDatabase.Key,
                            rollbackOperationGroupByContainer.Key,
                            rollbackOperationGroupByOperation.Key);

                        var packagePartitionOperationName = rollbackPackagePartition.OperationType.ToString().ToLowerInvariant();
                        var rollbackPackagePartitionUri = rollbackPackageModel.CreatePartition(rollbackPackagePartition);

                        var rollbackEntries = rollbackOperationGroupByOperation
                            .Select(static x => x.Document)
                            .ToArray();

                        for (var i = 0; i < rollbackEntries.Length; i++)
                        {
                            _logger.LogInformation(
                                "Packing cdbpkg:{PartitionKey}:$[{DocumentIndex}] for {OperationName} in {DatabaseName}\\{ContainerName}",
                                packagePartitionKey,
                                i,
                                packagePartitionOperationName,
                                rollbackPackagePartition.DatabaseName,
                                rollbackPackagePartition.ContainerName);
                        }

                        var rollbackPackagePart = rollbackPackage.CreatePart(rollbackPackagePartitionUri, "application/json", default);

                        using (var rollbackPackagePartStream = rollbackPackagePart.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(rollbackPackagePartStream, rollbackEntries, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await rollbackPackageModel.SaveAsync(cancellationToken).ConfigureAwait(false);

            rollbackPackage.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            rollbackPackage.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            rollbackPackage.PackageProperties.Created = DateTime.UtcNow;
        }
        catch
        {
            File.Delete(rollbackPackagePath);

            throw;
        }

        return true;
    }
}
