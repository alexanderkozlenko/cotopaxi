// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class CosmosMetadataCache : IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Dictionary<(string, string), JsonPointer[]> _partitionKeyPathsCache = new();

    public CosmosMetadataCache(CosmosClient cosmosClient)
    {
        Debug.Assert(cosmosClient is not null);

        _cosmosClient = cosmosClient;
    }

    public void Dispose()
    {
        _partitionKeyPathsCache.Clear();
    }

    public async Task<JsonPointer[]> GetPartitionKeyPathsAsync(string databaseName, string containerName, CancellationToken cancellationToken)
    {
        Debug.Assert(databaseName is not null);
        Debug.Assert(containerName is not null);

        var partitionKeyPathsKey = (databaseName, containerName);

        if (!_partitionKeyPathsCache.TryGetValue(partitionKeyPathsKey, out var partitionKeyPaths))
        {
            var container = _cosmosClient.GetContainer(databaseName, containerName);
            var containerResponse = default(ContainerResponse);

            try
            {
                containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);
            }
            catch (CosmosException ex)
                when ((int)ex.StatusCode == 404)
            {
                var message = $"The container '/{databaseName}/{containerName}' could not be found (status: {(int)ex.StatusCode}, sub-status: {ex.SubStatusCode}, activity: {ex.ActivityId})";

                throw new InvalidOperationException(message)
                {
                    HResult = ex.HResult,
                };
            }

            partitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();

            _partitionKeyPathsCache[partitionKeyPathsKey] = partitionKeyPaths;
        }

        return partitionKeyPaths;
    }
}
