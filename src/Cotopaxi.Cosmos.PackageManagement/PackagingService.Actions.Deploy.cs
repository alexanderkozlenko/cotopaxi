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

        var partitionKeyPathsRegistry = new Dictionary<(string, string), JsonPointer[]>();
        var packageIdentifiers = new HashSet<string>(packagePaths.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var packagePath in packagePaths)
        {
            if (!dryRun)
            {
                _logger.LogInformation("Deploying package {PackagePath} to {CosmosEndpoint}", packagePath, cosmosClient.Endpoint);
            }
            else
            {
                _logger.LogInformation("[dry-run] Deploying package {PackagePath} to {CosmosEndpoint}", packagePath, cosmosClient.Endpoint);
            }

            using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (string.IsNullOrEmpty(package.PackageProperties.Identifier))
            {
                throw new InvalidOperationException($"Cannot get package identifier for {packagePath}");
            }

            if (!packageIdentifiers.Add(package.PackageProperties.Identifier))
            {
                throw new InvalidOperationException($"Package {package.PackageProperties.Identifier} is already processed");
            }

            var packagePartitions = default(PackagePartition[]);

            using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
            {
                packagePartitions = packageModel.GetPartitions();
            }

            packagePartitions = packagePartitions
                .Where(static x =>
                    string.Equals(x.OperationName, "DELETE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.OperationName, "CREATE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.OperationName, "UPSERT", StringComparison.OrdinalIgnoreCase))
                .ToArray();

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

                    if (!partitionKeyPathsRegistry.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                    {
                        var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                        partitionKeyPathsRegistry[containerPartitionKeyPathsKey] = containerPartitionKeyPaths;

                        if (!dryRun)
                        {
                            _logger.LogInformation(
                               "Acquiring configuration for container {DatabaseName}\\{ContainerName} - HTTP {StatusCode} ({RU} RU)",
                               packagePartitionGroupByDatabase.Key,
                               packagePartitionGroupByContainer.Key,
                               (int)containerResponse.StatusCode,
                               Math.Round(containerResponse.RequestCharge, 2));
                        }
                        else
                        {
                            _logger.LogInformation(
                                "[dry-run] Acquiring configuration for container {DatabaseName}\\{ContainerName} - HTTP {StatusCode} ({RU} RU)",
                                packagePartitionGroupByDatabase.Key,
                                packagePartitionGroupByContainer.Key,
                                (int)containerResponse.StatusCode,
                                Math.Round(containerResponse.RequestCharge, 2));
                        }
                    }

                    var packagePartitionGroupsByOperation = packagePartitionGroupByContainer
                        .GroupBy(static x => x.OperationName, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(static x => x.Key, PackageOperationComparer.Instance);

                    foreach (var packagePartitionGroupByOperation in packagePartitionGroupsByOperation)
                    {
                        var packagePartitionsByOperation = packagePartitionGroupByOperation
                            .OrderBy(static x => x.PartitionUri.OriginalString, StringComparer.Ordinal);

                        foreach (var packagePartition in packagePartitionsByOperation)
                        {
                            if (!dryRun)
                            {
                                _logger.LogInformation(
                                    "Deploying partition {PartitionName} for {OperationName} in {DatabaseName}\\{ContainerName}",
                                    packagePartition.PartitionName,
                                    packagePartition.OperationName.ToUpperInvariant(),
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "[dry-run] Deploying partition {PartitionName} for {OperationName} in {DatabaseName}\\{ContainerName}",
                                    packagePartition.PartitionName,
                                    packagePartition.OperationName.ToUpperInvariant(),
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName);
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

                                document.Remove("_attachments");
                                document.Remove("_etag");
                                document.Remove("_rid");
                                document.Remove("_self");
                                document.Remove("_ts");

                                if (!CosmosDocument.TryGetUniqueID(document, out var documentUID))
                                {
                                    throw new InvalidOperationException($"Cannot get document identifier for {packagePartition.PartitionUri}:$[{i}]");
                                }

                                if (!CosmosDocument.TryGetPartitionKey(document, containerPartitionKeyPaths, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Cannot get document partition key for {packagePartition.PartitionUri}:$[{i}]");
                                }

                                if (!dryRun)
                                {
                                    try
                                    {
                                        var operationResponse = packagePartition.OperationName.ToUpperInvariant() switch
                                        {
                                            "DELETE" => await container.DeleteItemAsync<JsonObject?>(documentUID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            "CREATE" => await container.CreateItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            "UPSERT" => await container.UpsertItemAsync<JsonObject?>(document, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            _ => throw new NotSupportedException(),
                                        };

                                        _logger.LogInformation(
                                            "Executing {OperationName} {PartitionName}:$[{DocumentIndex}] - HTTP {StatusCode} ({RU} RU)",
                                            packagePartition.OperationName.ToUpperInvariant(),
                                            packagePartition.PartitionName,
                                            i,
                                            (int)operationResponse.StatusCode,
                                            Math.Round(operationResponse.RequestCharge, 2));

                                    }
                                    catch (CosmosException ex)
                                    {
                                        if ((string.Equals(packagePartition.OperationName, "DELETE", StringComparison.OrdinalIgnoreCase) && (ex.StatusCode == HttpStatusCode.NotFound)) ||
                                            (string.Equals(packagePartition.OperationName, "CREATE", StringComparison.OrdinalIgnoreCase) && (ex.StatusCode == HttpStatusCode.Conflict)))
                                        {
                                            _logger.LogWarning(
                                                "Executing {OperationName} {PartitionName}:$[{DocumentIndex}] - HTTP {StatusCode} ({RU} RU)",
                                                packagePartition.OperationName.ToUpperInvariant(),
                                                packagePartition.PartitionName,
                                                i,
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
                                    _logger.LogInformation(
                                        "[dry-run] Executing {OperationName} {PartitionName}:$[{DocumentIndex}] - HTTP ? (0 RU)",
                                        packagePartition.OperationName.ToUpperInvariant(),
                                        packagePartition.PartitionName,
                                        i);
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
