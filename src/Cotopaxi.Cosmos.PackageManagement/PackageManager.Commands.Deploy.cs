// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> DeployPackagesAsync(IReadOnlyCollection<string> packagePaths, CosmosAuthInfo cosmosAuthInfo, IReadOnlyCollection<string>? profilePaths, bool dryRun, CancellationToken cancellationToken)
    {
        Debug.Assert(packagePaths is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        if (packagePaths.Count == 0)
        {
            return true;
        }

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            EnableContentResponseOnWrite = false,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
        };

        using var cosmosClient = cosmosAuthInfo.IsConnectionString ?
            new CosmosClient(cosmosAuthInfo.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosAuthInfo.AccountEndpoint.AbsoluteUri, cosmosAuthInfo.AuthKeyOrResourceToken, cosmosClientOptions);

        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();
        var deployOperations = new HashSet<(PackageDocumentKey, PackageOperationType)>();
        var eligibleDocumentKeys = profilePaths is not null ? await GetEligibleDocumentKeysAsync(profilePaths, cancellationToken).ConfigureAwait(false) : null;

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
                                var document = documents[i];

                                if (document is null)
                                {
                                    continue;
                                }

                                CosmosDocument.Prune(document);

                                if (!CosmosDocument.TryGetId(document, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths, out var documentPartitionKey))
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

                                if (!dryRun)
                                {
                                    if ((eligibleDocumentKeys is null) || eligibleDocumentKeys.Contains(documentKey))
                                    {
                                        try
                                        {
                                            var operationResponse = default(ItemResponse<JsonObject?>);

                                            switch (packagePartition.OperationType)
                                            {
                                                case PackageOperationType.Delete:
                                                    {
                                                        operationResponse = await container.DeleteItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
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

                                                        operationResponse = await container.PatchItemAsync<JsonObject?>(documentId, documentPartitionKey, patchOperations, default, cancellationToken).ConfigureAwait(false);
                                                    }
                                                    break;
                                                default:
                                                    {
                                                        throw new NotSupportedException();
                                                    }
                                            }

                                            _logger.LogInformation(
                                                "{OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
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
                                            if ((((int)ex.StatusCode == 404) && (packagePartition.OperationType == PackageOperationType.Delete)) ||
                                                (((int)ex.StatusCode == 409) && (packagePartition.OperationType == PackageOperationType.Create)) ||
                                                (((int)ex.StatusCode == 404) && (packagePartition.OperationType == PackageOperationType.Patch)))
                                            {
                                                _logger.LogWarning(
                                                    "{OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}: HTTP {StatusCode} ({RU:F2} RU)",
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
                                }
                                else
                                {
                                    if ((eligibleDocumentKeys is null) || eligibleDocumentKeys.Contains(documentKey))
                                    {
                                        _logger.LogInformation(
                                            "[dry-run] {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}: HTTP ??? (?.?? RU)",
                                            packagePartitionOperationName,
                                            packagePartition.DatabaseName,
                                            packagePartition.ContainerName,
                                            documentId,
                                            documentPartitionKey);
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

    private static async Task<FrozenSet<PackageDocumentKey>> GetEligibleDocumentKeysAsync(IReadOnlyCollection<string> profilePaths, CancellationToken cancellationToken)
    {
        var documentKeys = new HashSet<PackageDocumentKey>();

        foreach (var profilePath in profilePaths)
        {
            var documentKeyNodes = default(PackageDocumentKeyNode?[]);

            using (var profileStream = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                documentKeyNodes = await JsonSerializer.DeserializeAsync<PackageDocumentKeyNode?[]>(profileStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            foreach (var documentKeyNode in documentKeyNodes.Where(static x => x is not null))
            {
                var documentKey = new PackageDocumentKey(
                    documentKeyNode!.DatabaseName.Value,
                    documentKeyNode!.ContainerName.Value,
                    documentKeyNode!.DocumentId.Value,
                    documentKeyNode!.DocumentPartitionKey);

                documentKeys.Add(documentKey);
            }
        }

        return documentKeys.ToFrozenSet();
    }
}
