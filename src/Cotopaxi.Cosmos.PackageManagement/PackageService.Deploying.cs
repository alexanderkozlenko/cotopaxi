// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Packaging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageService
{
    public async Task<bool> DeployPackageAsync(IReadOnlyCollection<FileInfo> packageFiles, CosmosCredential? cosmosCredential, CancellationToken cancellationToken)
    {
        Debug.Assert(packageFiles is not null);

        if (packageFiles.Count == 0)
        {
            _logger.LogInformation("No packages matching the specified pattern were found");

            return true;
        }

        var dryRun = !TryCreateCosmosClient(cosmosCredential, out var cosmosClient);
        var partitionKeyPathsRegistry = new Dictionary<(string, string), JsonPointer[]>();

        try
        {
            foreach (var packageFile in packageFiles)
            {
                if (!dryRun)
                {
                    _logger.LogInformation("Deploying package {FilePath} to {CosmosEndpoint}", packageFile.FullName, cosmosClient!.Endpoint);
                }
                else
                {
                    _logger.LogInformation("[DRY-RUN] Deploying package {FilePath}", packageFile.FullName);
                }

                var deploymentCompleted = false;
                var deploymentCharge = 0.0;

                try
                {
                    using (var package = Package.Open(packageFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var packageEntries = default(FrozenSet<PackageEntry>);
                        var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

                        await using (packageModel.ConfigureAwait(false))
                        {
                            packageEntries = packageModel.ExtractEntries();
                        }

                        var packageEntryGroupsByDatabase = packageEntries
                            .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                            .OrderBy(static x => x.Key, StringComparer.Ordinal);

                        foreach (var packageEntryGroupByDatabase in packageEntryGroupsByDatabase)
                        {
                            var packageEntryGroupsByContainer = packageEntryGroupByDatabase
                                .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                                .OrderBy(static x => x.Key, StringComparer.Ordinal);

                            foreach (var packageEntryGroupByContainer in packageEntryGroupsByContainer)
                            {
                                var container = default(Container);
                                var containerPartitionKeyPaths = default(JsonPointer[]);

                                if (!dryRun)
                                {
                                    container = cosmosClient!.GetContainer(packageEntryGroupByDatabase.Key, packageEntryGroupByContainer.Key);

                                    var containerPartitionKeyPathsKey = (packageEntryGroupByDatabase.Key, packageEntryGroupByContainer.Key);

                                    if (!partitionKeyPathsRegistry.TryGetValue(containerPartitionKeyPathsKey, out containerPartitionKeyPaths))
                                    {
                                        var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                                        deploymentCharge += containerResponse.RequestCharge;
                                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                                        partitionKeyPathsRegistry.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);

                                        _logger.LogInformation(
                                            "Acquiring partition key configuration for {DatabaseName}.{ContainerName} - OK (HTTP {StatusCode}, {RU} RU)",
                                            packageEntryGroupByDatabase.Key,
                                            packageEntryGroupByContainer.Key,
                                            (int)containerResponse.StatusCode,
                                            Math.Round(containerResponse.RequestCharge, 2));
                                    }
                                }

                                var packageEntryGroupsByOperation = packageEntryGroupByContainer
                                    .GroupBy(static x => x.OperationName, StringComparer.Ordinal)
                                    .OrderBy(static x => x.Key, PackageOperationComparer.Instance);

                                foreach (var packageEntryGroupByOperation in packageEntryGroupsByOperation)
                                {
                                    foreach (var packageEntry in packageEntryGroupByOperation.OrderBy(static x => x.UUID))
                                    {
                                        var packagePart = package.GetPart(new(packageEntry.SourcePath, UriKind.Relative));
                                        var documentNodes = default(JsonObject?[]);

                                        using (var packageEntryStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
                                        {
                                            documentNodes = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packageEntryStream, s_readJsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                                        }

                                        if (!dryRun)
                                        {
                                            _logger.LogInformation(
                                                "Deploying cdbpkg:{PackageEntryPath} to {DatabaseName}.{ContainerName}",
                                                packageEntry.SourcePath,
                                                packageEntry.DatabaseName,
                                                packageEntry.ContainerName);
                                        }
                                        else
                                        {
                                            _logger.LogInformation(
                                                "[DRY-RUN] Deploying cdbpkg:{PackageEntryPath} to {DatabaseName}.{ContainerName}",
                                                packageEntry.SourcePath,
                                                packageEntry.DatabaseName,
                                                packageEntry.ContainerName);
                                        }

                                        for (var i = 0; i < documentNodes.Length; i++)
                                        {
                                            var documentNode = documentNodes[i];

                                            if (documentNode is null)
                                            {
                                                continue;
                                            }

                                            if (!CosmosResource.TryGetDocumentID(documentNode, out var documentID))
                                            {
                                                if (!dryRun)
                                                {
                                                    _logger.LogError(
                                                        "Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - ERROR (the document doesn't have proper ID)",
                                                        packageEntry.OperationName,
                                                        packageEntry.SourcePath,
                                                        i);
                                                }
                                                else
                                                {
                                                    _logger.LogError(
                                                        "[DRY-RUN] Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - ERROR (the document doesn't have proper ID)",
                                                        packageEntry.OperationName,
                                                        packageEntry.SourcePath,
                                                        i);
                                                }

                                                return false;
                                            }

                                            if (!dryRun)
                                            {
                                                if (!CosmosResource.TryGetPartitionKey(documentNode, containerPartitionKeyPaths!, out var documentPartitionKey))
                                                {
                                                    _logger.LogError(
                                                        "Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - ERROR (the document doesn't have proper partition key)",
                                                        packageEntry.OperationName,
                                                        packageEntry.SourcePath,
                                                        i);

                                                    return false;
                                                }

                                                var documentResponse = default(ItemResponse<JsonObject>);

                                                try
                                                {
                                                    documentResponse = packageEntry.OperationName switch
                                                    {
                                                        "DELETE" => await container!.DeleteItemAsync<JsonObject>(documentID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                                        "CREATE" => await container!.CreateItemAsync(documentNode, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                                        "UPSERT" => await container!.UpsertItemAsync(documentNode, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                                        _ => default,
                                                    };

                                                    if (documentResponse is not null)
                                                    {
                                                        deploymentCharge += documentResponse.RequestCharge;

                                                        _logger.LogInformation(
                                                            "Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - OK (HTTP {StatusCode}, {RU} RU)",
                                                            packageEntry.OperationName,
                                                            packageEntry.SourcePath,
                                                            i,
                                                            (int)documentResponse.StatusCode,
                                                            Math.Round(documentResponse.RequestCharge, 2));
                                                    }
                                                }
                                                catch (CosmosException ex)
                                                {
                                                    deploymentCharge += ex.RequestCharge;

                                                    if (((packageEntry.OperationName == "DELETE") && (ex.StatusCode == HttpStatusCode.NotFound)) ||
                                                        ((packageEntry.OperationName == "CREATE") && (ex.StatusCode == HttpStatusCode.Conflict)))
                                                    {
                                                        _logger.LogInformation(
                                                            "Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - OK (HTTP {StatusCode}, {RU} RU)",
                                                            packageEntry.OperationName,
                                                            packageEntry.SourcePath,
                                                            i,
                                                            (int)ex.StatusCode,
                                                            Math.Round(ex.RequestCharge, 2));
                                                    }
                                                    else
                                                    {
                                                        _logger.LogError(
                                                            "Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - ERROR (HTTP {StatusCode}, {RU} RU, Activity {ActivityID})",
                                                            packageEntry.OperationName,
                                                            packageEntry.SourcePath,
                                                            i,
                                                            (int)ex.StatusCode,
                                                            Math.Round(ex.RequestCharge, 2),
                                                            ex.ActivityId);

                                                        return false;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogInformation(
                                                    "[DRY-RUN] Executing {OperationName} cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] ($.id: \"{DocumentID}\")",
                                                    packageEntry.OperationName,
                                                    packageEntry.SourcePath,
                                                    i,
                                                    documentID);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    deploymentCompleted = true;
                }
                finally
                {
                    if (!dryRun)
                    {
                        if (deploymentCompleted)
                        {
                            _logger.LogInformation("Successfully deployed package {FilePath} ({RU} RU)", packageFile.FullName, Math.Round(deploymentCharge, 2));
                        }
                        else
                        {
                            _logger.LogError("Failed to deploy package {FilePath} ({RU} RU)", packageFile.FullName, Math.Round(deploymentCharge, 2));
                        }
                    }
                    else
                    {
                        _logger.LogInformation("[DRY-RUN] Successfully deployed package {FilePath}", packageFile.FullName);
                    }
                }
            }
        }
        finally
        {
            cosmosClient?.Dispose();
        }

        return true;
    }

    private static bool TryCreateCosmosClient(CosmosCredential? credential, [NotNullWhen(true)] out CosmosClient? client)
    {
        if (credential is not null)
        {
            var cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationName = $"cotopaxi/{typeof(PackageService).Assembly.GetName().Version?.ToString(3)}",
                UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
                EnableContentResponseOnWrite = false,
            };

            client = credential.IsConnectionString ?
                new(credential.ConnectionString, cosmosClientOptions) :
                new(credential.AccountEndpoint.AbsoluteUri, credential.AuthKeyOrResourceToken, cosmosClientOptions);
        }
        else
        {
            client = null;
        }

        return client is not null;
    }
}
