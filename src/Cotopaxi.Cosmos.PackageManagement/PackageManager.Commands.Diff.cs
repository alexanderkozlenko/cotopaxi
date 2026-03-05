// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848
#pragma warning disable CA1869
#pragma warning disable CA1873

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public partial class PackageManager
{
    public async Task<bool> ComparePackagesAsync(string package1Path, string package2Path, CosmosAuthInfo cosmosAuthInfo, string? diffFilter, string? profilePath, bool useExitCode, CancellationToken cancellationToken)
    {
        Debug.Assert(package1Path is not null);
        Debug.Assert(package2Path is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo);
        using var cosmosMetadataCache = new CosmosMetadataCache(cosmosClient);

        var package1Documents = await GetPackageDocumentsAsync(package1Path, cosmosClient, cosmosMetadataCache, cancellationToken).ConfigureAwait(false);
        var package2Documents = await GetPackageDocumentsAsync(package2Path, cosmosClient, cosmosMetadataCache, cancellationToken).ConfigureAwait(false);

        var documentsCreated = package1Documents
            .Where(x => !package2Documents.ContainsKey(x.Key))
            .Select(static x => (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(x.Value, null)))
            .ToArray();

        var documentsUpdated = package1Documents
            .Where(x => package2Documents.ContainsKey(x.Key))
            .Select(x => (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(x.Value, package2Documents[x.Key])))
            .Where(static x => (x.Counters.Created > 0) || (x.Counters.Updated > 0) || (x.Counters.Deleted > 0))
            .ToArray();

        var documentsDeleted = package2Documents
            .Where(x => !package1Documents.ContainsKey(x.Key))
            .Select(static x => (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(null, x.Value)))
            .ToArray();

        if (IsIncludedDiffType(diffFilter, 'A'))
        {
            PrintCountersSection("+++", documentsCreated);
        }

        if (IsIncludedDiffType(diffFilter, 'M'))
        {
            PrintCountersSection("***", documentsUpdated);
        }

        if (IsIncludedDiffType(diffFilter, 'D'))
        {
            PrintCountersSection("---", documentsDeleted);
        }

        if (profilePath is not null)
        {
            var profileDocumentKeys = new HashSet<PackageDocumentKey>();

            profileDocumentKeys.UnionWith(documentsUpdated.Select(static x => x.DocumentKey));
            profileDocumentKeys.UnionWith(documentsCreated.Select(static x => x.DocumentKey));

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

            await using (var profileStream = new FileStream(profilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(profileStream, profileDocumentKeyNodes, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        if (useExitCode && (profilePath is null))
        {
            if ((documentsCreated.Length > 0) ||
                (documentsUpdated.Length > 0) ||
                (documentsDeleted.Length > 0))
            {
                return false;
            }
        }

        return true;

        static bool IsIncludedDiffType(string? diffFilter, char diffType)
        {
            return string.IsNullOrEmpty(diffFilter) || diffFilter.Contains(diffType, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void PrintCountersSection(string category, (PackageDocumentKey DocumentKey, DatabaseOperationType OperationType, DifferenceCounters Counters)[] source, bool printOperationName = true)
    {
        if (source.Length == 0)
        {
            return;
        }

        var printItems = source
            .Select(static x => (
                DatabaseName: x.DocumentKey.DatabaseName,
                ContainerName: x.DocumentKey.ContainerName,
                DocumentId: x.DocumentKey.DocumentId,
                DocumentPartitionKey: x.DocumentKey.DocumentPartitionKey.ToString(),
                OperationType: x.OperationType,
                Counters: x.Counters))
            .OrderBy(static x => x.OperationType)
            .ThenBy(static x => x.DatabaseName, StringComparer.Ordinal)
            .ThenBy(static x => x.ContainerName, StringComparer.Ordinal)
            .ThenBy(static x => x.DocumentId, StringComparer.Ordinal)
            .ThenBy(static x => x.DocumentPartitionKey, StringComparer.Ordinal)
            .ThenByDescending(static x => x.Counters.Created)
            .ThenByDescending(static x => x.Counters.Updated)
            .ThenByDescending(static x => x.Counters.Deleted)
            .ToArray();

        var printItemCountersBuilder = new StringBuilder();

        foreach (var printItem in printItems)
        {
            printItemCountersBuilder.Clear();

            if (printItem.Counters.Created > 0)
            {
                printItemCountersBuilder.AppendFormat(CultureInfo.InvariantCulture, "+{0} ", printItem.Counters.Created);
            }

            if (printItem.Counters.Updated > 0)
            {
                printItemCountersBuilder.AppendFormat(CultureInfo.InvariantCulture, "*{0} ", printItem.Counters.Updated);
            }

            if (printItem.Counters.Deleted > 0)
            {
                printItemCountersBuilder.AppendFormat(CultureInfo.InvariantCulture, "-{0} ", printItem.Counters.Deleted);
            }

            var printItemCounters = printItemCountersBuilder.ToString().TrimEnd();

            if (printOperationName)
            {
                var printItemOperationName = printItem.OperationType.ToString().ToLowerInvariant();

                _logger.LogInformation(
                    "{Category} {OperationName} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey} ({DocumentCounters})",
                    category,
                    printItemOperationName,
                    printItem.DatabaseName,
                    printItem.ContainerName,
                    printItem.DocumentId,
                    printItem.DocumentPartitionKey,
                    printItemCounters);
            }
            else
            {
                _logger.LogInformation(
                    "{Category} /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey} ({DocumentCounters})",
                    category,
                    printItem.DatabaseName,
                    printItem.ContainerName,
                    printItem.DocumentId,
                    printItem.DocumentPartitionKey,
                    printItemCounters);
            }
        }
    }

    private static DifferenceCounters CreateGlobalCounters(JsonObject? document1, JsonObject? document2)
    {
        if ((document1 is not null) && (document2 is null))
        {
            return new(document1.Count, 0, 0);
        }

        if ((document1 is null) && (document2 is not null))
        {
            return new(0, 0, document2.Count);
        }

        if ((document1 is not null) && (document2 is not null))
        {
            var countCreated = document1.Count(x => !document2.ContainsKey(x.Key));
            var countUpdated = document1.Count(x => document2.TryGetPropertyValue(x.Key, out var value) && !JsonNode.DeepEquals(x.Value, value));
            var countDeleted = document2.Count(x => !document1.ContainsKey(x.Key));

            return new(countCreated, countUpdated, countDeleted);
        }

        return new(0, 0, 0);
    }

    private static DifferenceCounters CreateScopedCounters(JsonObject? document1, JsonObject? document2)
    {
        if ((document1 is not null) && (document2 is null))
        {
            return new(document1.Count, 0, 0);
        }

        if ((document1 is null) && (document2 is not null))
        {
            return new(0, 0, 0);
        }

        if ((document1 is not null) && (document2 is not null))
        {
            var countCreated = document1.Count(x => !document2.ContainsKey(x.Key));
            var countUpdated = document1.Count(x => document2.TryGetPropertyValue(x.Key, out var value) && !JsonNode.DeepEquals(x.Value, value));

            return new(countCreated, countUpdated, 0);
        }

        return new(0, 0, 0);
    }

    private static async Task<FrozenDictionary<(PackageDocumentKey DocumentKey, DatabaseOperationType OperationType), JsonObject>> GetPackageDocumentsAsync(string packagePath, CosmosClient cosmosClient, CosmosMetadataCache cosmosMetadataCache, CancellationToken cancellationToken)
    {
        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Open, FileAccess.Read, cancellationToken).ConfigureAwait(false);

        var packagePartitions = package.GetPartitions();
        var packageDocuments = new Dictionary<(PackageDocumentKey DocumentKey, DatabaseOperationType OperationType), JsonObject>();

        foreach (var packagePartition in packagePartitions)
        {
            var containerPartitionKeyPaths = await cosmosMetadataCache.GetPartitionKeyPathsAsync(packagePartition.DatabaseName, packagePartition.ContainerName, cancellationToken).ConfigureAwait(false);
            var documents = default(JsonObject?[]);

            await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartitionStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
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
                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartition.Uri}:$[{i}]");
                }

                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths!, out var documentPartitionKey))
                {
                    throw new InvalidOperationException($"Failed to extract document partition key from {packagePartition.Uri}:$[{i}]");
                }

                var documentKey = new PackageDocumentKey(
                    packagePartition.DatabaseName,
                    packagePartition.ContainerName,
                    documentId,
                    documentPartitionKey);

                if (!packageDocuments.TryAdd((documentKey, packagePartition.OperationType), document))
                {
                    throw new InvalidOperationException($"A conflicting deployment entry at {packagePartition.Uri}:$[{i}]");
                }
            }
        }

        return packageDocuments.ToFrozenDictionary();
    }

    public async Task<bool> ComparePackageWithDatabaseAsync(string package1Path, CosmosAuthInfo cosmosAuthInfo, string? diffFilter, string? profilePath, bool useExitCode, CancellationToken cancellationToken)
    {
        Debug.Assert(package1Path is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo);
        using var cosmosMetadataCache = new CosmosMetadataCache(cosmosClient);

        var package1Documents = await GetPackageDocumentsAsync(package1Path, cosmosClient, cosmosMetadataCache, cancellationToken).ConfigureAwait(false);
        var databaseDocuments = await GetDatabaseDocumentsAsync(package1Path, cosmosClient, cosmosMetadataCache, cancellationToken).ConfigureAwait(false);

        var documentsCreated = package1Documents
            .Where(x => !databaseDocuments.ContainsKey(x.Key) && ((x.Key.OperationType == DatabaseOperationType.Create) || (x.Key.OperationType == DatabaseOperationType.Upsert)))
            .Select(static x => (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(x.Value, null)))
            .ToArray();

        var documentsUpdated = package1Documents
            .Where(x => databaseDocuments.ContainsKey(x.Key) && ((x.Key.OperationType == DatabaseOperationType.Upsert) || (x.Key.OperationType == DatabaseOperationType.Patch)))
            .Select(x =>
            {
                var packageDocument = x.Value;
                var databaseDocument = databaseDocuments[x.Key];

                if (x.Key.OperationType != DatabaseOperationType.Patch)
                {
                    return (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(packageDocument, databaseDocument));
                }
                else
                {
                    return (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateScopedCounters(packageDocument, databaseDocument));
                }
            })
            .Where(static x => (x.Counters.Created > 0) || (x.Counters.Updated > 0) || (x.Counters.Deleted > 0))
            .ToArray();

        var documentsDeleted = package1Documents
            .Where(x => databaseDocuments.ContainsKey(x.Key) && x.Key.OperationType == DatabaseOperationType.Delete)
            .Select(x => (x.Key.DocumentKey, x.Key.OperationType, Counters: CreateGlobalCounters(null, databaseDocuments[x.Key])))
            .ToArray();

        if (IsIncludedDiffType(diffFilter, 'A'))
        {
            PrintCountersSection("+++", documentsCreated, false);
        }

        if (IsIncludedDiffType(diffFilter, 'M'))
        {
            PrintCountersSection("***", documentsUpdated, false);
        }

        if (IsIncludedDiffType(diffFilter, 'D'))
        {
            PrintCountersSection("---", documentsDeleted, false);
        }

        if (profilePath is not null)
        {
            var profileDocumentKeys = new HashSet<PackageDocumentKey>();

            profileDocumentKeys.UnionWith(documentsUpdated.Select(static x => x.DocumentKey));
            profileDocumentKeys.UnionWith(documentsCreated.Select(static x => x.DocumentKey));
            profileDocumentKeys.UnionWith(documentsDeleted.Select(static x => x.DocumentKey));

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

            await using (var profileStream = new FileStream(profilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(profileStream, profileDocumentKeyNodes, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        if (useExitCode && (profilePath is null))
        {
            if ((documentsCreated.Length > 0) ||
                (documentsUpdated.Length > 0) ||
                (documentsDeleted.Length > 0))
            {
                return false;
            }
        }

        return true;

        static bool IsIncludedDiffType(string? diffFilter, char diffType)
        {
            return string.IsNullOrEmpty(diffFilter) || diffFilter.Contains(diffType, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<FrozenDictionary<(PackageDocumentKey DocumentKey, DatabaseOperationType OperationType), JsonObject>> GetDatabaseDocumentsAsync(string packagePath, CosmosClient cosmosClient, CosmosMetadataCache cosmosMetadataCache, CancellationToken cancellationToken)
    {
        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Open, FileAccess.Read, cancellationToken).ConfigureAwait(false);

        var packagePartitions = package.GetPartitions();
        var databaseDocuments = new Dictionary<(PackageDocumentKey DocumentKey, DatabaseOperationType OperationType), JsonObject>();

        foreach (var packagePartition in packagePartitions)
        {
            var containerPartitionKeyPaths = await cosmosMetadataCache.GetPartitionKeyPathsAsync(packagePartition.DatabaseName, packagePartition.ContainerName, cancellationToken).ConfigureAwait(false);
            var documents = default(JsonObject?[]);

            await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartitionStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            var container = cosmosClient.GetContainer(packagePartition.DatabaseName, packagePartition.ContainerName);

            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];

                if (document is null)
                {
                    continue;
                }

                if (!CosmosDocument.TryGetId(document, out var documentId))
                {
                    throw new InvalidOperationException($"Failed to extract document identifier from {packagePartition.Uri}:$[{i}]");
                }

                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths!, out var documentPartitionKey))
                {
                    throw new InvalidOperationException($"Failed to extract document partition key from {packagePartition.Uri}:$[{i}]");
                }

                var documentKey = new PackageDocumentKey(
                    packagePartition.DatabaseName,
                    packagePartition.ContainerName,
                    documentId,
                    documentPartitionKey);

                try
                {
                    var operationResponse = await container.ReadItemAsync<JsonObject?>(documentId, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);
                    var databaseDocument = operationResponse.Resource;

                    if (databaseDocument is not null)
                    {
                        CosmosDocument.PruneSystemProperties(databaseDocument);

                        databaseDocuments[(documentKey, packagePartition.OperationType)] = databaseDocument;
                    }
                }
                catch (CosmosException ex)
                    when ((int)ex.StatusCode == 404)
                {
                }
            }
        }

        return databaseDocuments.ToFrozenDictionary();
    }
}
