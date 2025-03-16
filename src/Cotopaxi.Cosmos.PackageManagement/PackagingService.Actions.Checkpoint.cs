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
    public async Task<bool> CreateCheckpointPackagesAsync(IReadOnlyCollection<string> sourcePackagePaths, string rollbackPackagePath, CosmosCredential cosmosCredential, CancellationToken cancellationToken)
    {
        Debug.Assert(sourcePackagePaths is not null);
        Debug.Assert(rollbackPackagePath is not null);
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

        var deployOperations = new HashSet<(PackageOperationKey, PackageOperationType)>();
        var partitionKeyPathsCache = new Dictionary<(string, string), JsonPointer[]>();
        var rollbackOperationSources = new Dictionary<PackageOperationKey, (Dictionary<PackageOperationType, JsonObject> SourceDocuments, JsonObject? TargetDocument)>();

        _logger.LogInformation("Building rollback package {TargetPath} for endpoint {CosmosEndpoint}", rollbackPackagePath, cosmosClient.Endpoint);

        foreach (var sourcePackagePath in sourcePackagePaths)
        {
            _logger.LogInformation("Analyzing deployment package {SourcePath}", sourcePackagePath);

            using var sourcePackage = Package.Open(sourcePackagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var sourcePackagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

            using (var sourcePackageModel = await PackageModel.OpenAsync(sourcePackage, default, cancellationToken).ConfigureAwait(false))
            {
                sourcePackagePartitions = sourcePackageModel.GetPartitions();
            }

            var sourcePackagePartitionGroupsByDatabase = sourcePackagePartitions
                .GroupBy(static x => x.Value.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var sourcePackagePartitionGroupByDatabase in sourcePackagePartitionGroupsByDatabase)
            {
                var sourcePackagePartitionGroupsByContainer = sourcePackagePartitionGroupByDatabase
                    .GroupBy(static x => x.Value.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var sourcePackagePartitionGroupByContainer in sourcePackagePartitionGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);
                    var containerPartitionKeyPathsKey = (sourcePackagePartitionGroupByDatabase.Key, sourcePackagePartitionGroupByContainer.Key);

                    if (!partitionKeyPathsCache.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                    {
                        var containerResponse = await container.ReadContainerAsync(default, cancellationToken).ConfigureAwait(false);

                        containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                        partitionKeyPathsCache.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);
                    }

                    var sourcePackagePartitionGroupsByOperation = sourcePackagePartitionGroupByContainer
                        .GroupBy(static x => x.Value.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var sourcePackagePartitionGroupByOperation in sourcePackagePartitionGroupsByOperation)
                    {
                        var sourcePackagePartitionsByOperation = sourcePackagePartitionGroupByOperation
                            .OrderBy(static x => x.Key.OriginalString, StringComparer.Ordinal);

                        foreach (var (sourcePackagePartitionUri, sourcePackagePartition) in sourcePackagePartitionsByOperation)
                        {
                            var sourcePackagePartitionOperationName = PackageOperation.Format(sourcePackagePartition.OperationType);

                            _logger.LogInformation(
                                "Analyzing deployment entries cdbpkg:{PartitionKey} for container {DatabaseName}\\{ContainerName} ({OperationName})",
                                sourcePackagePartition.PartitionKey,
                                sourcePackagePartition.DatabaseName,
                                sourcePackagePartition.ContainerName,
                                sourcePackagePartitionOperationName);

                            var sourcePackagePart = sourcePackage.GetPart(sourcePackagePartitionUri);
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

                                _logger.LogInformation("Analyzing deployment entry cdbpkg:{PartitionKey}:$[{DocumentIndex}]", sourcePackagePartition.PartitionKey, i);

                                CosmosResource.RemoveSystemProperties(sourceDocument);

                                if (!CosmosResource.TryGetDocumentID(sourceDocument, out var documentID))
                                {
                                    throw new InvalidOperationException($"Unable to get document identifier for cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                if (!CosmosResource.TryGetPartitionKey(sourceDocument, containerPartitionKeyPaths!, out var documentPartitionKey))
                                {
                                    throw new InvalidOperationException($"Unable to get document partition key for cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                var deployOperationKey = new PackageOperationKey(
                                    sourcePackagePartition.DatabaseName,
                                    sourcePackagePartition.ContainerName,
                                    documentID,
                                    documentPartitionKey);

                                if (!deployOperations.Add((deployOperationKey, sourcePackagePartition.OperationType)))
                                {
                                    throw new InvalidOperationException($"Unable to include duplicate deployment entry cdbpkg:{sourcePackagePartitionUri}:$[{i}]");
                                }

                                if (!rollbackOperationSources.TryGetValue(deployOperationKey, out var rollbackOperationSource))
                                {
                                    var targetDocument = default(JsonObject?);

                                    try
                                    {
                                        var operationResponse = await container.ReadItemAsync<JsonObject?>(documentID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                                        _logger.LogInformation(
                                            "Requesting document for deployment entry cdbpkg:{PartitionKey}:$[{DocumentIndex}] - HTTP {StatusCode}",
                                            sourcePackagePartition.PartitionKey,
                                            i,
                                            (int)operationResponse.StatusCode);

                                        targetDocument = operationResponse.Resource;
                                    }
                                    catch (CosmosException ex)
                                    {
                                        if (ex.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            _logger.LogInformation(
                                                "Requesting document for deployment entry cdbpkg:{PartitionKey}:$[{DocumentIndex}] - HTTP {StatusCode}",
                                                sourcePackagePartition.PartitionKey,
                                                i,
                                                (int)ex.StatusCode);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }

                                    if (targetDocument is not null)
                                    {
                                        CosmosResource.RemoveSystemProperties(targetDocument);
                                    }

                                    rollbackOperationSource = (new(), targetDocument);
                                    rollbackOperationSources.Add(deployOperationKey, rollbackOperationSource);
                                }

                                rollbackOperationSource.SourceDocuments.Add(sourcePackagePartition.OperationType, sourceDocument);
                            }
                        }
                    }
                }
            }
        }

        var rollbackOperations = new List<(string DatabaseName, string ContainerName, JsonObject Document, PackageOperationType OperationType)>();

        foreach (var (rollbackOperationKey, rollbackOperationValue) in rollbackOperationSources)
        {
            var sourceDocument = default(JsonObject);

            if (rollbackOperationValue.TargetDocument is null)
            {
                if (rollbackOperationValue.SourceDocuments.TryGetValue(PackageOperationType.Create, out sourceDocument) ||
                    rollbackOperationValue.SourceDocuments.TryGetValue(PackageOperationType.Upsert, out sourceDocument))
                {
                    var rollbackOperation = (
                        rollbackOperationKey.DatabaseName,
                        rollbackOperationKey.ContainerName,
                        sourceDocument,
                        PackageOperationType.Delete);

                    rollbackOperations.Add(rollbackOperation);
                }
            }
            else
            {
                if (rollbackOperationValue.SourceDocuments.ContainsKey(PackageOperationType.Delete) ||
                    rollbackOperationValue.SourceDocuments.ContainsKey(PackageOperationType.Upsert))
                {
                    var rollbackOperation = (
                        rollbackOperationKey.DatabaseName,
                        rollbackOperationKey.ContainerName,
                        rollbackOperationValue.TargetDocument,
                        PackageOperationType.Upsert);

                    rollbackOperations.Add(rollbackOperation);
                }
                else if (rollbackOperationValue.SourceDocuments.TryGetValue(PackageOperationType.Patch, out sourceDocument))
                {
                    var targetDocument = (JsonObject)rollbackOperationValue.TargetDocument.DeepClone();

                    var propertyNamesToExclude = targetDocument
                        .Where(x => !sourceDocument.ContainsKey(x.Key))
                        .Select(static x => x.Key)
                        .ToArray();

                    foreach (var propertyName in propertyNamesToExclude)
                    {
                        targetDocument.Remove(propertyName);
                    }

                    var rollbackOperation = (
                        rollbackOperationKey.DatabaseName,
                        rollbackOperationKey.ContainerName,
                        targetDocument,
                        PackageOperationType.Patch);

                    rollbackOperations.Add(rollbackOperation);
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(rollbackPackagePath)!);

        try
        {
            using var rollbackPackage = Package.Open(rollbackPackagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var rollbackPackageModel = await PackageModel.OpenAsync(rollbackPackage, default, cancellationToken).ConfigureAwait(false);

            var rollbackOperationGroupsByDatabase = rollbackOperations
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var rollbackOperationGroupByDatabase in rollbackOperationGroupsByDatabase)
            {
                var rollbackOperationGroupsByContainer = rollbackOperationGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var rollbackOperationGroupByContainer in rollbackOperationGroupsByContainer)
                {
                    var rollbackOperationGroupsByOperation = rollbackOperationGroupByContainer
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var rollbackOperationGroupByOperation in rollbackOperationGroupsByOperation)
                    {
                        var packagePartitionKey = Guid.CreateVersion7();

                        var rollbackPackagePartition = new PackagePartition(
                            packagePartitionKey,
                            rollbackOperationGroupByDatabase.Key,
                            rollbackOperationGroupByContainer.Key,
                            rollbackOperationGroupByOperation.Key);

                        var packagePartitionOperationName = PackageOperation.Format(rollbackPackagePartition.OperationType);

                        _logger.LogInformation(
                            "Packing rollback entries cdbpkg:{PartitionKey} for container {DatabaseName}\\{ContainerName} ({OperationName})",
                            packagePartitionKey,
                            rollbackPackagePartition.DatabaseName,
                            rollbackPackagePartition.ContainerName,
                            packagePartitionOperationName);

                        var rollbackPackagePartitionUri = rollbackPackageModel.CreatePartition(rollbackPackagePartition);

                        var rollbackEntries = rollbackOperationGroupByOperation
                            .Select(static x => x.Document)
                            .ToArray();

                        for (var i = 0; i < rollbackEntries.Length; i++)
                        {
                            _logger.LogInformation("Packing rollback entry cdbpkg:{PartitionKey}:$[{DocumentIndex}]", packagePartitionKey, i);
                        }

                        var rollbackPackagePart = rollbackPackage.CreatePart(rollbackPackagePartitionUri, "application/json", default);

                        using (var rollbackPackagePartStream = rollbackPackagePart.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(rollbackPackagePartStream, rollbackEntries, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await rollbackPackageModel.SaveAsync(cancellationToken).ConfigureAwait(false);

            rollbackPackage.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            rollbackPackage.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            rollbackPackage.PackageProperties.Created = DateTime.UtcNow;
        }
        catch
        {
            File.Delete(rollbackPackagePath);

            throw;
        }

        return true;
    }
}
