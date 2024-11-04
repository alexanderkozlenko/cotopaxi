// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.IO.Packaging;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class PackageService
{
    private static readonly JsonSerializerOptions s_readJsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger _logger;

    public PackageService(ILogger<PackageService> logger)
    {
        Debug.Assert(logger is not null);

        _logger = logger;
    }

    public async Task<bool> CreatePackageAsync(FileInfo projectFile, FileInfo packageFile, CancellationToken cancellationToken)
    {
        Debug.Assert(projectFile is not null);
        Debug.Assert(packageFile is not null);

        _logger.LogInformation("Reading {FilePath}", projectFile);

        var packageEntries = await CollectPackageEntriesAsync(projectFile, cancellationToken).ConfigureAwait(false);

        packageFile.Directory!.Create();

        using (var package = Package.Open(packageFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

            await using (packageModel.ConfigureAwait(false))
            {
                foreach (var packageEntry in packageEntries)
                {
                    _logger.LogInformation("Reading {FilePath}", packageEntry.SourcePath);

                    var documentNodes = default(JsonObject?[]);

                    using (var packageEntryStream = new FileStream(packageEntry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        documentNodes = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packageEntryStream, s_readJsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                    }

                    documentNodes = documentNodes.Where(static x => x is not null).ToArray();

                    if (documentNodes.Length == 0)
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Packing {PackageEntryUrn} for {OperationName} in {DatabaseName}.{ContainerName}",
                        GetPackageEntryUrn(packageEntry.UUID),
                        packageEntry.OperationName,
                        packageEntry.DatabaseName,
                        packageEntry.ContainerName);

                    for (var i = 0; i < documentNodes.Length; i++)
                    {
                        var documentNode = documentNodes[i]!;

                        if (!CosmosResource.TryGetUniqueID(documentNode, out var documentID) || !CosmosResource.IsProperUniqueID(documentID))
                        {
                            _logger.LogError("Packing {PackageDocumentUrn} - ERROR (the document doesn't have a proper ID)", GetPackageDocumentUrn(packageEntry.UUID, i));

                            return false;
                        }

                        CosmosResource.RemoveSystemProperties(documentNode);

                        _logger.LogInformation("Packing {PackageDocumentUrn} - OK", GetPackageDocumentUrn(packageEntry.UUID, i));
                    }

                    var packagePartPath = packageModel.CreateEntry(packageEntry.UUID, packageEntry);
                    var packagePart = package.CreatePart(new(packagePartPath, UriKind.Relative), "application/json", default);

                    using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        await JsonSerializer.SerializeAsync(packagePartStream, documentNodes, JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        _logger.LogInformation("Successfully created {FilePath}", packageFile.FullName);

        return true;
    }

    public async Task<bool> DeployPackageAsync(IReadOnlyCollection<FileInfo> packageFiles, CosmosCredential cosmosCredential, CancellationToken cancellationToken)
    {
        Debug.Assert(packageFiles is not null);
        Debug.Assert(cosmosCredential is not null);

        var applicationVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var cosmosClientOptions = new CosmosClientOptions
        {
            ApplicationName = $"cotopaxi/{applicationVersion}",
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
            EnableContentResponseOnWrite = false,
        };

        using var cosmosClient = cosmosCredential.IsConnectionString ?
            new CosmosClient(cosmosCredential.ConnectionString, cosmosClientOptions) :
            new CosmosClient(cosmosCredential.AccountEndpoint.AbsoluteUri, cosmosCredential.AuthKeyOrResourceToken, cosmosClientOptions);

        _logger.LogInformation("Deploying {PackageCount} packages to {CosmosEndpoint}", packageFiles.Count, cosmosClient.Endpoint.AbsoluteUri.TrimEnd('/'));

        var partitionKeyPathsRegistry = new Dictionary<(string, string), JsonPointer[]>();
        var deployCharge = 0.0;

        foreach (var packageFile in packageFiles)
        {
            _logger.LogInformation("Deploying {FilePath}", packageFile.FullName);

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
                        var container = cosmosClient.GetContainer(packageEntryGroupByDatabase.Key, packageEntryGroupByContainer.Key);
                        var containerPartitionKeyPathsKey = (packageEntryGroupByDatabase.Key, packageEntryGroupByContainer.Key);

                        if (!partitionKeyPathsRegistry.TryGetValue(containerPartitionKeyPathsKey, out var containerPartitionKeyPaths))
                        {
                            var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                            deployCharge += containerResponse.RequestCharge;
                            containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();
                            partitionKeyPathsRegistry.Add(containerPartitionKeyPathsKey, containerPartitionKeyPaths);

                            _logger.LogInformation(
                                "Acquiring partition key paths for {DatabaseName}.{ContainerName} - OK (HTTP {StatusCode}, {RU} RU)",
                                packageEntryGroupByDatabase.Key,
                                packageEntryGroupByContainer.Key,
                                (int)containerResponse.StatusCode,
                                Math.Round(containerResponse.RequestCharge, 2));
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
                                    documentNodes = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packageEntryStream, JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false) ?? [];
                                }

                                _logger.LogInformation(
                                    "Deploying {PackageEntryUrn} to {DatabaseName}.{ContainerName}",
                                    GetPackageEntryUrn(packageEntry.UUID),
                                    packageEntry.DatabaseName,
                                    packageEntry.ContainerName);

                                for (var i = 0; i < documentNodes.Length; i++)
                                {
                                    var documentNode = documentNodes[i];

                                    if (documentNode is null)
                                    {
                                        continue;
                                    }

                                    if (!CosmosResource.TryGetPartitionKey(documentNode, containerPartitionKeyPaths, out var documentPartitionKey) ||
                                        !CosmosResource.TryGetUniqueID(documentNode, out var documentID))
                                    {
                                        _logger.LogError(
                                            "Executing {OperationName} {PackageDocumentUrn} - ERROR (the document doesn't have proper partition key and ID)",
                                            packageEntry.OperationName,
                                            GetPackageDocumentUrn(packageEntry.UUID, i));

                                        _logger.LogError(
                                            "Aborted a deployment to {CosmosEndpoint} ({RU} RU)",
                                            cosmosClient.Endpoint.AbsoluteUri.TrimEnd('/'),
                                            Math.Round(deployCharge, 2));

                                        return false;
                                    }

                                    var documentResponse = default(ItemResponse<JsonObject>);

                                    try
                                    {
                                        documentResponse = packageEntry.OperationName switch
                                        {
                                            "DELETE" => await container.DeleteItemAsync<JsonObject>(documentID, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            "CREATE" => await container.CreateItemAsync(documentNode, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            "UPSERT" => await container.UpsertItemAsync(documentNode, documentPartitionKey, default, cancellationToken).ConfigureAwait(false),
                                            _ => default,
                                        };

                                        if (documentResponse is not null)
                                        {
                                            deployCharge += documentResponse.RequestCharge;

                                            _logger.LogInformation(
                                                "Executing {OperationName} {PackageDocumentUrn} - OK (HTTP {StatusCode}, {RU} RU)",
                                                packageEntry.OperationName,
                                                GetPackageDocumentUrn(packageEntry.UUID, i),
                                                (int)documentResponse.StatusCode,
                                                Math.Round(documentResponse.RequestCharge, 2));
                                        }
                                    }
                                    catch (CosmosException ex)
                                    {
                                        deployCharge += ex.RequestCharge;

                                        if (((packageEntry.OperationName == "DELETE") && (ex.StatusCode == HttpStatusCode.NotFound)) ||
                                            ((packageEntry.OperationName == "CREATE") && (ex.StatusCode == HttpStatusCode.Conflict)))
                                        {
                                            _logger.LogInformation(
                                                "Executing {OperationName} {PackageDocumentUrn} - OK (HTTP {StatusCode}, {RU} RU)",
                                                packageEntry.OperationName,
                                                GetPackageDocumentUrn(packageEntry.UUID, i),
                                                (int)ex.StatusCode,
                                                Math.Round(ex.RequestCharge, 2));
                                        }
                                        else
                                        {
                                            _logger.LogError(
                                                "Executing {OperationName} {PackageDocumentUrn} - ERROR (HTTP {StatusCode}, {RU} RU, Activity {ActivityID})",
                                                packageEntry.OperationName,
                                                GetPackageDocumentUrn(packageEntry.UUID, i),
                                                (int)ex.StatusCode,
                                                Math.Round(ex.RequestCharge, 2),
                                                ex.ActivityId);

                                            _logger.LogError(
                                                "Aborted a deployment to {CosmosEndpoint} ({RU} RU)",
                                                cosmosClient.Endpoint.AbsoluteUri.TrimEnd('/'),
                                                Math.Round(deployCharge, 2));

                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        _logger.LogInformation(
            "Successfully deployed {PackageCount} packages to {CosmosEndpoint} ({RU} RU)",
            packageFiles.Count,
            cosmosClient.Endpoint.AbsoluteUri.TrimEnd('/'),
            Math.Round(deployCharge, 2));

        return true;
    }

    private static async Task<FrozenSet<PackageEntry>> CollectPackageEntriesAsync(FileInfo projectFile, CancellationToken cancellationToken)
    {
        var packageEntries = new HashSet<PackageEntry>();
        var projectNode = default(ProjectNode);

        using (var projectStream = projectFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            projectNode = await JsonSerializer.DeserializeAsync<ProjectNode>(projectStream, s_readJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        if (projectNode is not null)
        {
            foreach (var projectDatabaseNode in projectNode.Databases.Where(static x => x is not null))
            {
                if (!CosmosResource.IsProperDatabaseName(projectDatabaseNode!.Name))
                {
                    throw new JsonException($"JSON deserialization for type '{typeof(ProjectDatabaseNode)}' encountered errors");
                }

                foreach (var projectContainerNode in projectDatabaseNode.Containers.Where(static x => x is not null))
                {
                    if (!CosmosResource.IsProperContainerName(projectContainerNode!.Name))
                    {
                        throw new JsonException($"JSON deserialization for type '{typeof(ProjectContainerNode)}' encountered errors");
                    }

                    foreach (var projectOperationNode in projectContainerNode.Operations.Where(static x => x is not null))
                    {
                        if (projectOperationNode!.Name is not { Length: > 0 })
                        {
                            throw new JsonException($"JSON deserialization for type '{typeof(ProjectOperationNode)}' encountered errors");
                        }

                        foreach (var collectionPathPattern in projectOperationNode.Documents.Where(static x => x is not null))
                        {
                            var collectionPaths = FindMatchingFiles(projectFile.Directory!, collectionPathPattern!);

                            foreach (var collectionPath in collectionPaths)
                            {
                                var packageEntryKey = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}:{1}:{2}:{3}",
                                    projectDatabaseNode.Name,
                                    projectContainerNode.Name,
                                    projectOperationNode.Name.ToUpperInvariant(),
                                    Path.GetRelativePath(projectFile.DirectoryName!, collectionPath));

                                var packageEntryUUID = GetPackageEntryUUID(packageEntryKey);

                                var packageEntry = new PackageEntry(
                                    packageEntryUUID,
                                    projectDatabaseNode.Name,
                                    projectContainerNode.Name,
                                    projectOperationNode.Name.ToUpperInvariant(),
                                    collectionPath);

                                packageEntries.Add(packageEntry);
                            }
                        }
                    }
                }
            }
        }

        return packageEntries.ToFrozenSet();
    }

    private static string[] FindMatchingFiles(DirectoryInfo directory, string pattern)
    {
        var matcher = new Matcher().AddInclude(pattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(directory));

        return match.Files.Select(x => Path.GetFullPath(Path.Combine(directory.FullName, x.Path))).ToArray();
    }

    private static Guid GetPackageEntryUUID(string source)
    {
        // UUID Version 8 (RFC 9562) - XXH128

        var hash = XxHash128.Hash(Encoding.Unicode.GetBytes(source));

        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new(hash);
    }

    private static string GetPackageEntryUrn(Guid packageEntryUUID)
    {
        return FormattableString.Invariant($"urn:cdbpkg:{packageEntryUUID}");
    }

    private static string GetPackageDocumentUrn(Guid packageEntryUUID, int documentIndex)
    {
        return FormattableString.Invariant($"urn:cdbpkg:{packageEntryUUID}:{documentIndex}");
    }
}
