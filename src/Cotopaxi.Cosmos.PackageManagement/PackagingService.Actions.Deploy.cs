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

        var deployOperations = new HashSet<(PackageOperationKey, PackageOperationType)>();
        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();

        _logger.LogInformation("Deploying packages for endpoint {CosmosEndpoint}", cosmosClient.Endpoint);

        foreach (var packagePath in packagePaths)
        {
            if (!dryRun)
            {
                _logger.LogInformation("Deploying package {PackagePath}", packagePath);
            }
            else
            {
                _logger.LogInformation("[dry-run] Deploying package {PackagePath}", packagePath);
            }

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
                        partitionKeyPathsCache[containerPartitionKeyPathsKey] = containerPartitionKeyPaths;
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
                            var packagePartitionOperationName = PackageOperation.Format(packagePartition.OperationType);

                            if (!dryRun)
                            {
                                _logger.LogInformation(
                                    "Deploying entries cdbpkg:{PartitionKey} to container {DatabaseName}\\{ContainerName} ({OperationName})",
                                    packagePartition.PartitionKey,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    packagePartitionOperationName);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "[dry-run] Deploying entries cdbpkg:{PartitionKey} to container {DatabaseName}\\{ContainerName} ({OperationName})",
                                    packagePartition.PartitionKey,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    packagePartitionOperationName);
                            }

                            var packagePart = package.GetPart(packagePartitionUri);
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
                                    throw new InvalidOperationException($"Unable to get document identifier for cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosResource.TryGetPartitionKey(document, containerPartitionKeyPaths, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Unable to get document partition key for cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                var deployOperationKey = new PackageOperationKey(
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentID,
                                    documentPartitionKey);

                                if (!deployOperations.Add((deployOperationKey, packagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"Unable to include duplicate deployment entry cdbpkg:{packagePartitionUri}:$[{i}]");
                                }

                                if (!dryRun)
                                {
                                    try
                                    {
                                        var operationResponse = default(ItemResponse<JsonObject?>);

                                        switch (packagePartition.OperationType)
                                        {
                                            case PackageOperationType.Delete:
                                                {
                                                    operationResponse = await container.DeleteItemAsync<JsonObject?>(documentID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case PackageOperationType.Create:
                                                {
                                                    operationResponse = await container.CreateItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case PackageOperationType.Upsert:
                                                {
                                                    operationResponse = await container.UpsertItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                                                }
                                                break;
                                            case PackageOperationType.Patch:
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
                                            "Deploying entry cdbpkg:{PartitionKey}:$[{DocumentIndex}] ({OperationName}) - HTTP {StatusCode}",
                                            packagePartition.PartitionKey,
                                            i,
                                            packagePartitionOperationName,
                                            (int)operationResponse.StatusCode);

                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (((packagePartition.OperationType == PackageOperationType.Create) && (ex.StatusCode == HttpStatusCode.Conflict)) ||
                                            ((packagePartition.OperationType == PackageOperationType.Patch) && (ex.StatusCode == HttpStatusCode.NotFound)) ||
                                            ((packagePartition.OperationType == PackageOperationType.Delete) && (ex.StatusCode == HttpStatusCode.NotFound)))
                                        {
                                            _logger.LogWarning(
                                                "Deploying entry cdbpkg:{PartitionKey}:$[{DocumentIndex}] ({OperationName}) - HTTP {StatusCode}",
                                                packagePartition.PartitionKey,
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
                                        "[dry-run] Deploying entry cdbpkg:{PartitionKey}:$[{DocumentIndex}] ({OperationName}) - HTTP ???",
                                        packagePartition.PartitionKey,
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
