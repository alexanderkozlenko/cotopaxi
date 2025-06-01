// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class CosmosMetadataCache
{
    private readonly CosmosClient _cosmosClient;
    private readonly Dictionary<(string, string), JsonPointer[]> _partitionKeyPathsCache = new();

    public CosmosMetadataCache(CosmosClient cosmosClient)
    {
        Debug.Assert(cosmosClient is not null);

        _cosmosClient = cosmosClient;
    }

    public async Task<JsonPointer[]> GetPartitionKeyPathsAsync(string databaseName, string containerName, CancellationToken cancellationToken)
    {
        Debug.Assert(databaseName is not null);
        Debug.Assert(containerName is not null);

        var partitionKeyPathsKey = (databaseName, containerName);

        if (!_partitionKeyPathsCache.TryGetValue(partitionKeyPathsKey, out var partitionKeyPaths))
        {
            var container = _cosmosClient.GetContainer(databaseName, containerName);
            var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

            partitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();

            _partitionKeyPathsCache[partitionKeyPathsKey] = partitionKeyPaths;
        }

        return partitionKeyPaths;
    }
}
