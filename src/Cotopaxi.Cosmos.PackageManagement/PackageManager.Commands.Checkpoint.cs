// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Primitives;
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

        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo);
        using var cosmosMetadataCache = new CosmosMetadataCache(cosmosClient);
        using var versionBuilder = new HashBuilder("SHA1");

        var deployOperations = new HashSet<(PackageDocumentKey, DatabaseOperationType)>();
        var deployDocumentStates = new Dictionary<PackageDocumentKey, (Dictionary<DatabaseOperationType, JsonObject> Sources, JsonObject? Target)>();

        foreach (var packagePath in sourcePackagePaths)
        {
            _logger.LogInformation("{SourcePath} + {CosmosEndpoint} >>> {TargetPath}", packagePath, cosmosClient.Endpoint, rollbackPackagePath);

            await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Open, FileAccess.Read, cancellationToken).ConfigureAwait(false);

            var packagePartitions = package.GetPartitions();

            var packagePartitionGroupsByDatabase = packagePartitions
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var packagePartitionGroupByDatabase in packagePartitionGroupsByDatabase)
            {
                var packagePartitionGroupsByContainer = packagePartitionGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var packagePartitionGroupByContainer in packagePartitionGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(packagePartitionGroupByDatabase.Key, packagePartitionGroupByContainer.Key);
                    var containerPartitionKeyPaths = await cosmosMetadataCache.GetPartitionKeyPathsAsync(packagePartitionGroupByDatabase.Key, packagePartitionGroupByContainer.Key, cancellationToken).ConfigureAwait(false);

                    var packagePartitionGroupsByOperation = packagePartitionGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var packagePartitionGroupByOperation in packagePartitionGroupsByOperation)
                    {
                        var packagePartitionsByOperation = packagePartitionGroupByOperation
                            .OrderBy(static x => x.Uri.OriginalString, StringComparer.Ordinal);

                        foreach (var packagePartition in packagePartitionsByOperation)
                        {
                            var documents = default(JsonObject?[]);

                            await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartitionStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            deployOperations.EnsureCapacity(deployOperations.Count + documents.Length);

                            for (var i = 0; i < documents.Length; i++)
                            {
                                var sourceDocument = documents[i];

                                if (sourceDocument is null)
                                {
                                    continue;
                                }

                                CosmosDocument.PruneSystemProperties(sourceDocument);

                                if (!CosmosDocument.TryGetId(sourceDocument, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartition.Uri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Failed to extract document partition key from {packagePartition.Uri}:$[{i}]");
                                }

                                var documentKey = new PackageDocumentKey(
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentId,
                                    documentPartitionKey);

                                if (!deployOperations.Add((documentKey, packagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"A duplicate document+operation entry {packagePartition.Uri}:$[{i}]");
                                }

                                if (!deployDocumentStates.TryGetValue(documentKey, out var deployDocumentState))
                                {
                                    var targetDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            documentId,
                                            documentPartitionKey,
                                            (int)operationResponse.StatusCode);

                                        targetDocument = operationResponse.Resource;
                                        versionBuilder.Append(operationResponse.ETag);
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if ((int)ex.StatusCode == 404)
                                        {
                                            _logger.LogInformation(
                                                "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                                packagePartition.DatabaseName,
                                                packagePartition.ContainerName,
                                                documentId,
                                                documentPartitionKey,
                                                (int)ex.StatusCode);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }

                                    if (targetDocument is not null)
                                    {
                                        CosmosDocument.PruneSystemProperties(targetDocument);
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

        var rollbackOperations = new List<(string DatabaseName, string ContainerName, JsonObject Document, DatabaseOperationType OperationType)>();

        foreach (var (documentKey, documentState) in deployDocumentStates)
        {
            var sourceDocument = default(JsonObject);
            var targetDocument = documentState.Target;

            if (targetDocument is null)
            {
                if (documentState.Sources.TryGetValue(DatabaseOperationType.Create, out sourceDocument) ||
                    documentState.Sources.TryGetValue(DatabaseOperationType.Upsert, out sourceDocument))
                {
                    var rollbackOperation = (
                        documentKey.DatabaseName,
                        documentKey.ContainerName,
                        sourceDocument,
                        DatabaseOperationType.Delete);

                    rollbackOperations.Add(rollbackOperation);
                }
            }
            else
            {
                if (documentState.Sources.ContainsKey(DatabaseOperationType.Delete))
                {
                    var rollbackOperation = (
                        documentKey.DatabaseName,
                        documentKey.ContainerName,
                        targetDocument,
                        DatabaseOperationType.Upsert);

                    rollbackOperations.Add(rollbackOperation);

                    continue;
                }

                if (documentState.Sources.TryGetValue(DatabaseOperationType.Upsert, out sourceDocument))
                {
                    var rollbackRequired = documentState.Sources.ContainsKey(DatabaseOperationType.Patch) || !JsonNode.DeepEquals(sourceDocument, targetDocument);

                    if (rollbackRequired)
                    {
                        var rollbackOperation = (
                            documentKey.DatabaseName,
                            documentKey.ContainerName,
                            targetDocument,
                            DatabaseOperationType.Upsert);

                        rollbackOperations.Add(rollbackOperation);
                    }

                    continue;
                }

                if (documentState.Sources.TryGetValue(DatabaseOperationType.Patch, out sourceDocument))
                {
                    var rollbackRequired = sourceDocument.Any(x => !targetDocument.TryGetPropertyValue(x.Key, out var v) || !JsonNode.DeepEquals(x.Value, v));

                    if (rollbackRequired)
                    {
                        var rollbackOperation = (
                            documentKey.DatabaseName,
                            documentKey.ContainerName,
                            targetDocument,
                            DatabaseOperationType.Upsert);

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
            await using var packageStream = new FileStream(rollbackPackagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Create, FileAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

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
                    var containerPartitionKeyPaths = await cosmosMetadataCache.GetPartitionKeyPathsAsync(operationGroupByDatabase.Key, operationGroupByContainer.Key, cancellationToken).ConfigureAwait(false);

                    var operationGroupsByOperation = operationGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var operationGroupByOperation in operationGroupsByOperation)
                    {
                        var packagePartition = package.CreatePartition(
                            operationGroupByDatabase.Key,
                            operationGroupByContainer.Key,
                            operationGroupByOperation.Key);

                        var packagePartitionOperationName = packagePartition.OperationType.ToString().ToLowerInvariant();

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

                        await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(packagePartitionStream, documents, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            package.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            package.PackageProperties.Version = versionBuilder.ToHashString();
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
