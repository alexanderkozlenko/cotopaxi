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
    public async Task<bool> CreateCheckpointPackagesAsync(IReadOnlyCollection<string> sourcePackagePaths, string revertPackagePath, CosmosCredential cosmosCredential, CancellationToken cancellationToken)
    {
        Debug.Assert(sourcePackagePaths is not null);
        Debug.Assert(revertPackagePath is not null);
        Debug.Assert(cosmosCredential is not null);

        if (sourcePackagePaths.Count == 0)
        {
            _logger.LogInformation("Packages matching the specified pattern were not found");

            return true;
        }

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
        };

        using var cosmosClient = cosmosCredential.IsConnectionString ?
            new CosmosClient(cosmosCredential.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosCredential.AccountEndpoint.AbsoluteUri, cosmosCredential.AuthKeyOrResourceToken, cosmosClientOptions);

        _logger.LogInformation("Building rollback package {RevertPath} using {CosmosEndpoint}", revertPackagePath, cosmosClient.Endpoint);

        var partitionKeyPathsRegistry = new Dictionary<(string, string), JsonPointer[]>();

        Directory.CreateDirectory(Path.GetDirectoryName(revertPackagePath)!);

        try
        {
            using var revertPackage = Package.Open(revertPackagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var revertPackageModel = await PackageModel.OpenAsync(revertPackage, default, cancellationToken).ConfigureAwait(false);

            revertPackage.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            revertPackage.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            revertPackage.PackageProperties.Created = DateTime.UtcNow;

            var sourcePackageIdentifiers = new HashSet<string>(sourcePackagePaths.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePackagePath in sourcePackagePaths)
            {
                _logger.LogInformation("Adding rollback operations for package {SourcePath}", sourcePackagePath);

                using var sourcePackage = Package.Open(sourcePackagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (string.IsNullOrEmpty(sourcePackage.PackageProperties.Identifier))
                {
                    throw new InvalidOperationException($"Cannot get package identifier for {sourcePackagePath}");
                }

                if (!sourcePackageIdentifiers.Add(sourcePackage.PackageProperties.Identifier))
                {
                    throw new InvalidOperationException($"Package {sourcePackage.PackageProperties.Identifier} is already processed");
                }

                var sourcePackagePartitions = default(PackagePartition[]);

                using (var sourcePackageModel = await PackageModel.OpenAsync(sourcePackage, default, cancellationToken).ConfigureAwait(false))
                {
                    sourcePackagePartitions = sourcePackageModel.GetPartitions();
                }

                sourcePackagePartitions = sourcePackagePartitions
                    .Where(static x =>
                        string.Equals(x.OperationName, "DELETE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.OperationName, "CREATE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.OperationName, "UPSERT", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                var sourcePackagePartitionGroupsByDatabase = sourcePackagePartitions
                    .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var sourcePackagePartitionGroupByDatabase in sourcePackagePartitionGroupsByDatabase)
                {
                    var sourcePackagePartitionGroupsByContainer = sourcePackagePartitionGroupByDatabase
                        .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                        .OrderBy(static x => x.Key, StringComparer.Ordinal);

                    foreach (var sourcePackagePartitionGroupByContainer in sourcePackagePartitionGroupsByContainer)
                    {
                        var container = cosmosClient.GetContainer(sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);
                        var containerPartitionKeyPathsKey = (sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);

                        if (!partitionKeyPathsRegistry.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                        {
                            var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                            containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                            partitionKeyPathsRegistry.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);

                            _logger.LogInformation(
                                "Acquiring configuration for container {DatabaseName}\\{ContainerName} - HTTP {StatusCode} ({RU} RU)",
                                sourcePackagePartitionGroupByDatabase.Key,
                                sourcePackagePartitionGroupByContainer.Key,
                                (int)containerResponse.StatusCode,
                                Math.Round(containerResponse.RequestCharge, 2));
                        }

                        var documentsToDelete = new List<JsonObject>();
                        var documentsToUpsert = new List<JsonObject>();

                        var sourcePackagePartitionGroupsByOperation = sourcePackagePartitionGroupByContainer
                            .GroupBy(static x => x.OperationName, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(static x => x.Key, PackageOperationComparer.Instance);

                        foreach (var sourcePackagePartitionGroupByOperation in sourcePackagePartitionGroupsByOperation)
                        {
                            var sourcePackagePartitionsByOperation = sourcePackagePartitionGroupByOperation
                                .OrderBy(static x => x.PartitionUri.OriginalString, StringComparer.Ordinal);

                            foreach (var sourcePackagePartition in sourcePackagePartitionsByOperation)
                            {
                                _logger.LogInformation(
                                    "Fetching snapshots for partition {PartitionName} from {DatabaseName}\\{ContainerName}",
                                    sourcePackagePartition.PartitionName,
                                    sourcePackagePartition.DatabaseName,
                                    sourcePackagePartition.ContainerName);

                                var sourcePackagePart = sourcePackage.GetPart(sourcePackagePartition.PartitionUri);
                                var sourceDocuments = default(JsonObject?[]);

                                using (var sourcePackagePartStream = sourcePackagePart.GetStream(FileMode.Open, FileAccess.Read))
                                {
                                    sourceDocuments = await JsonSerializer.DeserializeAsync<JsonObject?[]>(sourcePackagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                                }

                                for (var i = 0; i < sourceDocuments.Length; i++)
                                {
                                    var sourceDocument = sourceDocuments[i];

                                    if (sourceDocument is null)
                                    {
                                        continue;
                                    }

                                    sourceDocument.Remove("_attachments");
                                    sourceDocument.Remove("_etag");
                                    sourceDocument.Remove("_rid");
                                    sourceDocument.Remove("_self");
                                    sourceDocument.Remove("_ts");

                                    if (!CosmosDocument.TryGetUniqueID(sourceDocument, out var documentUID))
                                    {
                                        throw new InvalidOperationException($"Cannot get document identifier for {sourcePackagePartition.PartitionUri}:$[{i}]");
                                    }

                                    if (!CosmosDocument.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                    {
                                        throw new InvalidOperationException($"Cannot get document partition key for {sourcePackagePartition.PartitionUri}:$[{i}]");
                                    }

                                    var currentDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentUID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "Fetching snapshot for document {PartitionName}:$[{DocumentIndex}] - HTTP {StatusCode} ({RU} RU)",
                                            sourcePackagePartition.PartitionName,
                                            i,
                                            (int)operationResponse.StatusCode,
                                            Math.Round(operationResponse.RequestCharge, 2));

                                        currentDocument = operationResponse.Resource;
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (ex.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            _logger.LogInformation(
                                                "Fetching snapshot for document {PartitionName}:$[{DocumentIndex}] - HTTP {StatusCode} ({RU} RU)",
                                                sourcePackagePartition.PartitionName,
                                                i,
                                                (int)ex.StatusCode,
                                                Math.Round(ex.RequestCharge, 2));
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }

                                    if (currentDocument is not null)
                                    {
                                        currentDocument.Remove("_attachments");
                                        currentDocument.Remove("_rid");
                                        currentDocument.Remove("_self");
                                    }

                                    switch (sourcePackagePartition.OperationName.ToUpperInvariant())
                                    {
                                        case "DELETE":
                                            {
                                                if (currentDocument is not null)
                                                {
                                                    documentsToUpsert.Add(currentDocument);
                                                }
                                            }
                                            break;
                                        case "CREATE":
                                            {
                                                if (currentDocument is null)
                                                {
                                                    documentsToDelete.Add(sourceDocument);
                                                }
                                            }
                                            break;
                                        case "UPSERT":
                                            {
                                                if (currentDocument is not null)
                                                {
                                                    documentsToUpsert.Add(currentDocument);
                                                }
                                                else
                                                {
                                                    documentsToDelete.Add(sourceDocument);
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }

                        if (documentsToDelete.Count > 0)
                        {
                            var revertPackagePartitionUri = revertPackageModel.CreatePartition(
                                Guid.CreateVersion7().ToString(),
                                sourcePackagePartitionGroupByDatabase.Key,
                                sourcePackagePartitionGroupByContainer.Key,
                                "delete");

                            var revertPackagePart = revertPackage.CreatePart(revertPackagePartitionUri, "application/json", default);

                            using (var revertPackagePartStream = revertPackagePart.GetStream(FileMode.Create, FileAccess.Write))
                            {
                                await JsonSerializer.SerializeAsync(revertPackagePartStream, documentsToDelete, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        if (documentsToUpsert.Count > 0)
                        {
                            var revertPackagePartitionUri = revertPackageModel.CreatePartition(
                                Guid.CreateVersion7().ToString(),
                                sourcePackagePartitionGroupByDatabase.Key,
                                sourcePackagePartitionGroupByContainer.Key,
                                "upsert");

                            var revertPackagePart = revertPackage.CreatePart(revertPackagePartitionUri, "application/json", default);

                            using (var revertPackagePartStream = revertPackagePart.GetStream(FileMode.Create, FileAccess.Write))
                            {
                                await JsonSerializer.SerializeAsync(revertPackagePartStream, documentsToUpsert, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            await revertPackageModel.SaveAsync().ConfigureAwait(false);

            revertPackage.PackageProperties.Modified = DateTime.UtcNow;
            revertPackage.PackageProperties.Description = string.Join(';', sourcePackageIdentifiers);
        }
        catch
        {
            File.Delete(revertPackagePath);

            throw;
        }

        return true;
    }
}
