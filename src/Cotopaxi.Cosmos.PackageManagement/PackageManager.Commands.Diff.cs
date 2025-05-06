// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848
#pragma warning disable CA1869

using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> ComparePackagesAsync(string package1Path, string package2Path, CosmosAuthInfo cosmosAuthInfo, string? profilePath, bool useExitCode, CancellationToken cancellationToken)
    {
        Debug.Assert(package1Path is not null);
        Debug.Assert(package2Path is not null);
        Debug.Assert(cosmosAuthInfo is not null);

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
        var package1Documents = await GetPackageDocumentsAsync(package1Path, cosmosClient, partitionKeyPathsCache, cancellationToken).ConfigureAwait(false);
        var package2Documents = await GetPackageDocumentsAsync(package2Path, cosmosClient, partitionKeyPathsCache, cancellationToken).ConfigureAwait(false);

        var packageItemsDeleted = package2Documents
            .Where(x => !package1Documents.ContainsKey(x.Key))
            .Select(static x => x.Key)
            .ToArray();

        var packageItemsUpdated = package2Documents
            .Where(x => package1Documents.TryGetValue(x.Key, out var value) && !JsonNode.DeepEquals(x.Value, value))
            .Select(static x => x.Key)
            .ToArray();

        var packageItemsCreated = package1Documents
            .Where(x => !package2Documents.ContainsKey(x.Key))
            .Select(static x => x.Key)
            .ToArray();

        PrintDiffSection("---", packageItemsDeleted);
        PrintDiffSection("***", packageItemsUpdated);
        PrintDiffSection("+++", packageItemsCreated);

        if (profilePath is not null)
        {
            var profileDocumentKeys = new HashSet<PackageDocumentKey>();

            profileDocumentKeys.UnionWith(packageItemsUpdated.Select(static x => x.DocumentKey));
            profileDocumentKeys.UnionWith(packageItemsCreated.Select(static x => x.DocumentKey));

            var profileDocumentKeyNodes = profileDocumentKeys
                .OrderBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .ThenBy(static x => x.ContainerName, StringComparer.Ordinal)
                .ThenBy(static x => x.DocumentId, StringComparer.Ordinal)
                .ThenBy(static x => x.DocumentPartitionKey.ToString(), StringComparer.Ordinal)
                .Select(static x => new PackageDocumentKeyNode
                {
                    DatabaseName = new(x.DatabaseName),
                    ContainerName = new(x.ContainerName),
                    DocumentId = new(x.DocumentId),
                    DocumentPartitionKey = x.DocumentPartitionKey,
                })
                .ToArray();

            var jsonSerializerOptions = new JsonSerializerOptions(s_jsonSerializerOptions)
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            };

            using (var profileStream = new FileStream(profilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(profileStream, profileDocumentKeyNodes, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        if (useExitCode && (profilePath is null))
        {
            if ((packageItemsDeleted.Length > 0) ||
                (packageItemsUpdated.Length > 0) ||
                (packageItemsCreated.Length > 0))
            {
                return false;
            }
        }

        return true;
    }

    private void PrintDiffSection(string category, IEnumerable<(PackageDocumentKey DocumentKey, PackageOperationType OperationType)> source)
    {
        var printItems = source
            .Select(static x => (
                DatabaseName: x.DocumentKey.DatabaseName,
                ContainerName: x.DocumentKey.ContainerName,
                DocumentId: x.DocumentKey.DocumentId,
                DocumentPartitionKey: x.DocumentKey.DocumentPartitionKey.ToString(),
                OperationType: x.OperationType))
            .OrderBy(static x => x.DatabaseName, StringComparer.Ordinal)
            .ThenBy(static x => x.ContainerName, StringComparer.Ordinal)
            .ThenBy(static x => x.DocumentId, StringComparer.Ordinal)
            .ThenBy(static x => x.DocumentPartitionKey, StringComparer.Ordinal)
            .ThenBy(static x => x.OperationType)
            .ToArray();

        foreach (var printItem in printItems)
        {
            _logger.LogInformation(
                "{Category} {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} {DocumentPartitionKey}",
                category,
                printItem.OperationType.ToString().ToLowerInvariant().PadRight(6),
                printItem.DatabaseName,
                printItem.ContainerName,
                printItem.DocumentId,
                printItem.DocumentPartitionKey);
        }
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

        foreach (var (packagePartitionUri, packagePartition) in packagePartitions)
        {
            var container = cosmosClient.GetContainer(packagePartition.DatabaseName, packagePartition.ContainerName);
            var containerPartitionKeyPathsKey = (packagePartition.DatabaseName, packagePartition.ContainerName);

            if (!partitionKeyPathsCache.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
            {
                var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

                containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                partitionKeyPathsCache.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);
            }

            var packagePart = package.GetPart(packagePartitionUri);
            var documents = default(JsonObject?[]);

            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            packageDocuments.EnsureCapacity(packageDocuments.Count + documents.Length);

            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];

                if (document is null)
                {
                    continue;
                }

                if (!CosmosDocument.TryGetId(document, out var documentId))
                {
                    throw new InvalidOperationException($"Failed to extract document identifier from cdbpkg:{packagePartitionUri}:$[{i}]");
                }

                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths!, out var documentPartitionKey))
                {
                    throw new InvalidOperationException($"Failed to extract document partition key from cdbpkg:{packagePartitionUri}:$[{i}]");
                }

                var documentKey = new PackageDocumentKey(
                    packagePartition.DatabaseName,
                    packagePartition.ContainerName,
                    documentId,
                    documentPartitionKey);

                if (!packageDocuments.TryAdd((documentKey, packagePartition.OperationType), document))
                {
                    throw new InvalidOperationException($"A duplicate document+operation entry cdbpkg:{packagePartitionUri}:$[{i}]");
                }
            }
        }

        return packageDocuments.ToFrozenDictionary();
    }
}
