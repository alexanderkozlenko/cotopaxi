// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackagingService
{
    public async Task<bool> DeployPackagesAsync(IReadOnlyCollection<string> packagePaths, CosmosCredential cosmosCredential, bool dryRun, CancellationToken cancellationToken)
    {
        Debug.Assert(packagePaths is not null);
        Debug.Assert(cosmosCredential is not null);

        if (packagePaths.Count == 0)
        {
            _logger.LogInformation("Packages matching the specified pattern were not found");

            return true;
        }

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
            EnableContentResponseOnWrite = false,
        };

        using var cosmosClient = cosmosCredential.IsConnectionString ?
            new CosmosClient(cosmosCredential.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosCredential.AccountEndpoint.AbsoluteUri, cosmosCredential.AuthKeyOrResourceToken, cosmosClientOptions);

        var cosmosAccount = await cosmosClient.ReadAccountAsync().ConfigureAwait(false);
        var deployOperations = new HashSet<(PackageOperationKey, CosmosOperationType)>();
        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();

        foreach (var packagePath in packagePaths)
        {
            if (!dryRun)
            {
                _logger.LogInformation("Deploying package {PackagePath} to account {CosmosAccount}", packagePath, cosmosAccount.Id);
            }
            else
            {
                _logger.LogInformation("[dry-run] Deploying package {PackagePath} to account {CosmosAccount}", packagePath, cosmosAccount.Id);
            }

            using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var packagePartitions = default(PackagePartition[]);

            using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
            {
                packagePartitions = packageModel.GetPartitions();
            }

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
                    var containerPartitionKeyPathsKey = (packagePartitionGroupByDatabase.Key, packagePartitionGroupByContainer.Key);

                    if (!partitionKeyPathsCache.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                    {
                        var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                        partitionKeyPathsCache[containerPartitionKeyPathsKey] = containerPartitionKeyPaths;

                        if (!dryRun)
                        {
                            _logger.LogInformation(
                                "Requesting properties for container {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                packagePartitionGroupByDatabase.Key,
                                packagePartitionGroupByContainer.Key,
                                (int)containerResponse.StatusCode);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "[dry-run] Requesting properties for container {DatabaseName}\\{ContainerName} - HTTP {StatusCode}",
                                packagePartitionGroupByDatabase.Key,
                                packagePartitionGroupByContainer.Key,
                                (int)containerResponse.StatusCode);
                        }
                    }

                    var packagePartitionGroupsByOperation = packagePartitionGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var packagePartitionGroupByOperation in packagePartitionGroupsByOperation)
                    {
                        var packagePartitionsByOperation = packagePartitionGroupByOperation
                            .OrderBy(static x => x.PartitionUri.OriginalString, StringComparer.Ordinal);

                        foreach (var packagePartition in packagePartitionsByOperation)
                        {
                            var packagePartitionOperationName = CosmosOperation.Format(packagePartition.OperationType);

                            if (!dryRun)
                            {
                                _logger.LogInformation(
                                    "Deploying entries cdbpkg:{PartitionName} to container {DatabaseName}\\{ContainerName} ({OperationName})",
                                    packagePartition.PartitionName,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    packagePartitionOperationName);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "[dry-run] Deploying entries cdbpkg:{PartitionName} to container {DatabaseName}\\{ContainerName} ({OperationName})",
                                    packagePartition.PartitionName,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    packagePartitionOperationName);
                            }

                            var packagePart = package.GetPart(packagePartition.PartitionUri);
                            var documents = default(JsonObject?[]);

                            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            for (var i = 0; i < documents.Length; i++)
                            {
                                var document = documents[i];

                                if (document is null)
                                {
                                    continue;
                                }

                                CosmosResource.RemoveSystemProperties(document);

                                if (!CosmosResource.TryGetDocumentID(document, out var documentID))
                                {
                                    throw new InvalidOperationException($"Unable to get document identifier for cdbpkg:{packagePartition.PartitionUri}:$[{i}]");
                                }

                                if (!CosmosResource.TryGetPartitionKey(document, containerPartitionKeyPaths, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Unable to get document partition key for cdbpkg:{packagePartition.PartitionUri}:$[{i}]");
                                }

                                var deployOperationKey = new PackageOperationKey(
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentID,
                                    documentPartitionKey);

                                if (!deployOperations.Add((deployOperationKey, packagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"Unable to include duplicate deployment entry cdbpkg:{packagePartition.PartitionUri}:$[{i}]");
                                }

                                if (!dryRun)
                                {
                                    try
                                    {
                                        var operationResponse = default(ItemResponse<JsonObject?>);

                                        switch (packagePartition.OperationType)
                                        {
                                            case CosmosOperationType.Delete:
                                                {
                                                    operationResponse = await container.DeleteItemAsync<JsonObject?>(documentID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case CosmosOperationType.Create:
                                                {
                                                    operationResponse = await container.CreateItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case CosmosOperationType.Upsert:
                                                {
                                                    operationResponse = await container.UpsertItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case CosmosOperationType.Patch:
                                                {
                                                    var patchOperations = document
                                                        .Where(static x => x.Key != "id")
                                                        .Select(static x => PatchOperation.Set("/" + x.Key, x.Value))
                                                        .ToArray();

                                                    operationResponse = await container.PatchItemAsync<JsonObject?>(documentID, documentPartitionKey, patchOperations, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            default:
                                                {
                                                    throw new NotSupportedException();
                                                }
                                        }

                                        _logger.LogInformation(
                                            "Deploying entry cdbpkg:{PartitionName}:$[{DocumentIndex}] ({OperationName}) - HTTP {StatusCode}",
                                            packagePartition.PartitionName,
                                            i,
                                            packagePartitionOperationName,
                                            (int)operationResponse.StatusCode);

                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (((packagePartition.OperationType == CosmosOperationType.Create) && (ex.StatusCode == HttpStatusCode.Conflict)) ||
                                            ((packagePartition.OperationType == CosmosOperationType.Patch) && (ex.StatusCode == HttpStatusCode.NotFound)) ||
                                            ((packagePartition.OperationType == CosmosOperationType.Delete) && (ex.StatusCode == HttpStatusCode.NotFound)))
                                        {
                                            _logger.LogWarning(
                                                "Deploying entry cdbpkg:{PartitionName}:$[{DocumentIndex}] ({OperationName}) - HTTP {StatusCode}",
                                                packagePartition.PartitionName,
                                                i,
                                                packagePartitionOperationName,
                                                (int)ex.StatusCode);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        "[dry-run] Deploying entry cdbpkg:{PartitionName}:$[{DocumentIndex}] ({OperationName}) - HTTP ???",
                                        packagePartition.PartitionName,
                                        i,
                                        packagePartitionOperationName);
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
