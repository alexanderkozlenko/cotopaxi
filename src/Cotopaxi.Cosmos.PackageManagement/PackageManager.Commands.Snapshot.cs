﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Primitives;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public partial class PackageManager
{
    public async Task<bool> CreateSnapshotPackageAsync(IReadOnlyCollection<string> profilePaths, string packagePath, CosmosAuthInfo cosmosAuthInfo, CancellationToken cancellationToken)
    {
        Debug.Assert(profilePaths is not null);
        Debug.Assert(packagePath is not null);
        Debug.Assert(cosmosAuthInfo is not null);

        using var cosmosClient = CreateCosmosClient(cosmosAuthInfo);
        using var cosmosMetadataCache = new CosmosMetadataCache(cosmosClient);
        using var versionBuilder = new HashBuilder("SHA1", Encoding.UTF8);

        var profileDocumentKeys = await GetProfileDocumentKeysAsync(profilePaths, cancellationToken).ConfigureAwait(false);
        var packageDirectory = Path.GetDirectoryName(packagePath);

        _logger.LogInformation("{CosmosEndpoint} >>> {PackagePath}", cosmosClient.Endpoint, packagePath);

        if (packageDirectory is not null)
        {
            Directory.CreateDirectory(packageDirectory);
        }

        try
        {
            await using var packageStream = new FileStream(packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Create, FileAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

            var documentKeyGroupsByDatabase = profileDocumentKeys
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var documentKeyGroupByDatabase in documentKeyGroupsByDatabase)
            {
                var documentKeyGroupsByContainer = documentKeyGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var documentKeyGroupByContainer in documentKeyGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(documentKeyGroupByDatabase.Key, documentKeyGroupByContainer.Key);
                    var containerPartitionKeyPaths = await cosmosMetadataCache.GetPartitionKeyPathsAsync(documentKeyGroupByDatabase.Key, documentKeyGroupByContainer.Key, cancellationToken).ConfigureAwait(false);
                    var snapshots = new Dictionary<PackageDocumentKey, JsonObject>();

                    foreach (var documentKey in documentKeyGroupByContainer)
                    {
                        try
                        {
                            var operationResponse = await container.ReadItemAsync<JsonObject?>(documentKey.DocumentId, documentKey.DocumentPartitionKey, default, cancellationToken).ConfigureAwait(false);

                            _logger.LogInformation(
                                "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                documentKey.DatabaseName,
                                documentKey.ContainerName,
                                documentKey.DocumentId,
                                documentKey.DocumentPartitionKey,
                                (int)operationResponse.StatusCode);

                            var document = operationResponse.Resource;

                            if (document is not null)
                            {
                                CosmosDocument.PruneSystemProperties(document);

                                snapshots.Add(documentKey, document);
                                versionBuilder.Append(operationResponse.ETag);
                            }
                        }
                        catch (CosmosException ex)
                        {
                            if ((int)ex.StatusCode == 404)
                            {
                                _logger.LogInformation(
                                    "read /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey}: HTTP {StatusCode}",
                                    documentKey.DatabaseName,
                                    documentKey.ContainerName,
                                    documentKey.DocumentId,
                                    documentKey.DocumentPartitionKey,
                                    (int)ex.StatusCode);
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }

                    if (snapshots.Count == 0)
                    {
                        continue;
                    }

                    var packagePartition = package.CreatePartition(
                        documentKeyGroupByDatabase.Key,
                        documentKeyGroupByContainer.Key,
                        DatabaseOperationType.Upsert);

                    var documents = snapshots
                        .OrderBy(static x => x.Key.DocumentId, StringComparer.Ordinal)
                        .ThenBy(static x => x.Key.DocumentPartitionKey.ToString(), StringComparer.Ordinal)
                        .ToArray();

                    for (var i = 0; i < documents.Length; i++)
                    {
                        var (documentKey, document) = documents[i];

                        _logger.LogInformation(
                            "+++ upsert /{DatabaseName}/{ContainerName}/{DocumentId}:{DocumentPartitionKey} ({PropertyCount})",
                            packagePartition.DatabaseName,
                            packagePartition.ContainerName,
                            documentKey.DocumentId,
                            documentKey.DocumentPartitionKey,
                            document.Count);
                    }

                    await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        await JsonSerializer.SerializeAsync(packagePartitionStream, documents.Select(static x => x.Value), s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            package.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            package.PackageProperties.Version = versionBuilder.ToHashString();
            package.PackageProperties.Subject = cosmosClient.Endpoint.AbsoluteUri;
            package.PackageProperties.Created = _timeProvider.GetUtcNow().UtcDateTime;
            package.PackageProperties.Creator = s_applicationName;
        }
        catch (Exception ex)
        {
            try
            {
                File.Delete(packagePath);
            }
            catch (Exception exio)
            {
                throw new AggregateException(ex, exio);
            }

            throw;
        }

        return true;
    }
}
