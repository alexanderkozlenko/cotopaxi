// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
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
    public async Task<bool> ComparePackagesAsync(string package1Path, string package2Path, CosmosAuthInfo cosmosAuthInfo, bool useExitCode, CancellationToken cancellationToken)
    {
        Debug.Assert(package1Path is not null);
        Debug.Assert(package2Path is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
            EnableContentResponseOnWrite = false,
        };

        using var cosmosClient = cosmosAuthInfo.IsConnectionString ?
            new CosmosClient(cosmosAuthInfo.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosAuthInfo.AccountEndpoint.AbsoluteUri, cosmosAuthInfo.AuthKeyOrResourceToken, cosmosClientOptions);

        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();
        var package1Documents = await GetPackageDocumentsAsync(package1Path, cosmosClient, partitionKeyPathsCache, cancellationToken).ConfigureAwait(false);
        var package2Documents = await GetPackageDocumentsAsync(package2Path, cosmosClient, partitionKeyPathsCache, cancellationToken).ConfigureAwait(false);

        var packageItemsRemoved = package2Documents
            .Where(x => !package1Documents.ContainsKey(x.Key))
            .Select(static x => x.Key)
            .OrderBy(static x => x.DocumentKey.DatabaseName)
            .ThenBy(static x => x.DocumentKey.ContainerName)
            .ThenBy(static x => x.OperationType)
            .ThenBy(static x => x.DocumentKey.DocumentId)
            .ThenBy(static x => x.DocumentKey.DocumentPartitionKey.ToString())
            .ToArray();

        var packageItemsUpdated = package2Documents
            .Where(x => package1Documents.TryGetValue(x.Key, out var value) && !JsonNode.DeepEquals(x.Value, value))
            .Select(static x => x.Key)
            .OrderBy(static x => x.DocumentKey.DatabaseName)
            .ThenBy(static x => x.DocumentKey.ContainerName)
            .ThenBy(static x => x.OperationType)
            .ThenBy(static x => x.DocumentKey.DocumentId)
            .ThenBy(static x => x.DocumentKey.DocumentPartitionKey.ToString())
            .ToArray();

        var packageItemsAdded = package1Documents
            .Where(x => !package2Documents.ContainsKey(x.Key))
            .Select(static x => x.Key)
            .OrderBy(static x => x.DocumentKey.DatabaseName)
            .ThenBy(static x => x.DocumentKey.ContainerName)
            .ThenBy(static x => x.OperationType)
            .ThenBy(static x => x.DocumentKey.DocumentId)
            .ThenBy(static x => x.DocumentKey.DocumentPartitionKey.ToString())
            .ToArray();

        foreach (var (documentKey, operationType) in packageItemsRemoved)
        {
            _logger.LogInformation(
                "- {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}",
                operationType.ToString().ToLowerInvariant(),
                documentKey.DatabaseName,
                documentKey.ContainerName,
                documentKey.DocumentId,
                documentKey.DocumentPartitionKey);
        }

        foreach (var (documentKey, operationType) in packageItemsUpdated)
        {
            _logger.LogInformation(
                "* {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}",
                operationType.ToString().ToLowerInvariant(),
                documentKey.DatabaseName,
                documentKey.ContainerName,
                documentKey.DocumentId,
                documentKey.DocumentPartitionKey);
        }

        foreach (var (documentKey, operationType) in packageItemsAdded)
        {
            _logger.LogInformation(
                "+ {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}",
                operationType.ToString().ToLowerInvariant(),
                documentKey.DatabaseName,
                documentKey.ContainerName,
                documentKey.DocumentId,
                documentKey.DocumentPartitionKey);
        }

        if (useExitCode)
        {
            if ((packageItemsRemoved.Length != 0) ||
                (packageItemsUpdated.Length != 0) ||
                (packageItemsAdded.Length != 0))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<FrozenDictionary<(PackageDocumentKey DocumentKey, PackageOperationType OperationType), JsonObject>> GetPackageDocumentsAsync(string packagePath, CosmosClient cosmosClient, Dictionary<(string, string), JsonPointer[]> partitionKeyPathsCache, CancellationToken cancellationToken)
    {
        using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var packagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

        using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
        {
            packagePartitions = packageModel.GetPartitions();
        }

        var packageDocuments = new Dictionary<(PackageDocumentKey DocumentKey, PackageOperationType OperationType), JsonObject>();

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

                        using (var package1PartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
                        {
                            documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(package1PartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                        }

                        packageDocuments.EnsureCapacity(packageDocuments.Count + documents.Length);

                        for (var i = 0; i < documents.Length; i++)
                        {
                            var document = documents[i];

                            if (document is null)
                            {
                                continue;
                            }

                            if (!CosmosResource.TryGetDocumentId(document, out var documentId))
                            {
                                throw new InvalidOperationException($"Unable to get document identifier for cdbpkg:{packagePartitionUri}:$[{i}]");
                            }

                            if (!CosmosResource.TryGetPartitionKey(document, containerPartitionKeyPaths!, out var documentPartitionKey))
                            {
                                throw new InvalidOperationException($"Unable to get document partition key for cdbpkg:{packagePartitionUri}:$[{i}]");
                            }

                            var documentKey = new PackageDocumentKey(
                                packagePartition.DatabaseName,
                                packagePartition.ContainerName,
                                documentId,
                                documentPartitionKey);

                            if (!packageDocuments.TryAdd((documentKey, packagePartition.OperationType), document))
                            {
                                throw new InvalidOperationException($"Unable to include duplicate entry cdbpkg:{packagePartitionUri}:$[{i}]");
                            }
                        }
                    }
                }
            }
        }

        return packageDocuments.ToFrozenDictionary();
    }
}
