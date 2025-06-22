// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public partial class PackageManager
{
    public async Task<bool> DeployPackagesAsync(IReadOnlyCollection<string> packagePaths, CosmosAuthInfo cosmosAuthInfo, IReadOnlyCollection<string>? profilePaths, bool dryRun, CancellationToken cancellationToken)
    {
        Debug.Assert(packagePaths is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        if (packagePaths.Count == 0)
        {
            return true;
        }

        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo, static x => x.EnableContentResponseOnWrite = false);
        using var cosmosMetadataCache = new CosmosMetadataCache(cosmosClient);

        var deployOperations = new HashSet<(PackageDocumentKey, DatabaseOperationType)>();
        var profileDocumentKeys = profilePaths is not null ? await GetProfileDocumentKeysAsync(profilePaths, cancellationToken).ConfigureAwait(false) : null;

        foreach (var packagePath in packagePaths)
        {
            if (!dryRun)
            {
                _logger.LogInformation("{PackagePath} >>> {CosmosEndpoint}", packagePath, cosmosClient.Endpoint);
            }
            else
            {
                _logger.LogInformation("[dry-run] {PackagePath} >>> {CosmosEndpoint}", packagePath, cosmosClient.Endpoint);
            }

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
                            var packagePartitionOperationName = packagePartition.OperationType.ToString().ToLowerInvariant();
                            var documents = default(JsonObject?[]);

                            await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartitionStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            deployOperations.EnsureCapacity(deployOperations.Count + documents.Length);

                            for (var i = 0; i < documents.Length; i++)
                            {
                                var document = documents[i];

                                if (document is null)
                                {
                                    continue;
                                }

                                CosmosDocument.PruneSystemProperties(document);

                                if (!CosmosDocument.TryGetId(document, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartition.Uri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths, out var documentPartitionKey))
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

                                if ((profileDocumentKeys is not null) && !profileDocumentKeys.Contains(documentKey))
                                {
                                    continue;
                                }

                                if (!dryRun)
                                {
                                    try
                                    {
                                        var operationResponse = default(ItemResponse<JsonObject?>);

                                        switch (packagePartition.OperationType)
                                        {
                                            case DatabaseOperationType.Delete:
                                                {
                                                    operationResponse = await container.DeleteItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case DatabaseOperationType.Create:
                                                {
                                                    operationResponse = await container.CreateItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case DatabaseOperationType.Upsert:
                                                {
                                                    operationResponse = await container.UpsertItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case DatabaseOperationType.Patch:
                                                {
                                                    var patchOperations = document
                                                        .Where(static x => x.Key != "id")
                                                        .Select(static x => PatchOperation.Set("/" + x.Key, x.Value))
                                                        .ToArray();

                                                    operationResponse = await container.PatchItemAsync<JsonObject?>(documentId, documentPartitionKey, patchOperations, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            default:
                                                {
                                                    throw new InvalidOperationException();
                                                }
                                        }

                                        _logger.LogInformation(
                                            "{OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
                                            packagePartitionOperationName,
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            documentId,
                                            documentPartitionKey,
                                            (int)operationResponse.StatusCode,
                                            Math.Round(operationResponse.RequestCharge, 2));

                                    }
                                    catch (CosmosException ex)
                                    {
                                        if ((((int)ex.StatusCode == 404) && (packagePartition.OperationType == DatabaseOperationType.Delete)) ||
                                            (((int)ex.StatusCode == 409) && (packagePartition.OperationType == DatabaseOperationType.Create)) ||
                                            (((int)ex.StatusCode == 404) && (packagePartition.OperationType == DatabaseOperationType.Patch)))
                                        {
                                            _logger.LogInformation(
                                                "{OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
                                                packagePartitionOperationName,
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
                                }
                                else
                                {
                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        var statusCode = packagePartition.OperationType switch
                                        {
                                            DatabaseOperationType.Create => 409,
                                            DatabaseOperationType.Delete => 204,
                                            DatabaseOperationType.Upsert => 200,
                                            DatabaseOperationType.Patch => 200,
                                            _ => throw new InvalidOperationException(),
                                        };

                                        _logger.LogInformation(
                                            "[dry-run] {OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                            packagePartitionOperationName,
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            documentId,
                                            documentPartitionKey,
                                            statusCode);
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if ((int)ex.StatusCode == 404)
                                        {
                                            var statusCode = packagePartition.OperationType switch
                                            {
                                                DatabaseOperationType.Create => 201,
                                                DatabaseOperationType.Delete => 404,
                                                DatabaseOperationType.Upsert => 201,
                                                DatabaseOperationType.Patch => 404,
                                                _ => throw new InvalidOperationException(),
                                            };

                                            _logger.LogInformation(
                                                "[dry-run] {OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                                packagePartitionOperationName,
                                                packagePartition.DatabaseName,
                                                packagePartition.ContainerName,
                                                documentId,
                                                documentPartitionKey,
                                                statusCode);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return true;
    }
}
