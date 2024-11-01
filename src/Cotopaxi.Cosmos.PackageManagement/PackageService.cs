// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.IO.Packaging;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
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
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async Task<bool> CreatePackageAsync(string projectPath, string packagePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(packagePath);

        _logger.LogInformation("Reading {FilePath}", projectPath);

        var packageEntries = default(List<PackageEntry>);

        try
        {
            packageEntries = await CollectPackageEntriesAsync(projectPath, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Reading error: {Message}", ex.Message);

            return false;
        }

        using (var package = Package.Open(packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
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
                        try
                        {
                            documentNodes = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packageEntryStream, s_readJsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Reading error: {Message}", ex.Message);

                            return false;
                        }
                    }

                    documentNodes = documentNodes.Where(static x => x is not null).ToArray();

                    if (documentNodes.Length == 0)
                    {
                        continue;
                    }

                    var packageEntryKey = Guid.NewGuid();

                    _logger.LogInformation(
                        "Packing {PackageEntryUrn} for {OperationName} in {DatabaseName}\\{ContainerName}",
                        GetPackageEntryUrn(packageEntryKey),
                        packageEntry.OperationName,
                        packageEntry.DatabaseName,
                        packageEntry.ContainerName);

                    for (var i = 0; i < documentNodes.Length; i++)
                    {
                        var documentNode = documentNodes[i]!;

                        if (!CosmosResource.TryGetUniqueID(documentNode, out var documentID) || !CosmosResource.IsProperUniqueID(documentID))
                        {
                            _logger.LogError("Packing {PackageDocumentUrn} - ERROR (the document doesn't have a proper ID)", GetPackageDocumentUrn(packageEntryKey, i));

                            return false;
                        }

                        CosmosResource.RemoveSystemProperties(documentNode);

                        _logger.LogInformation("Packing {PackageDocumentUrn} - OK", GetPackageDocumentUrn(packageEntryKey, i));
                    }

                    var packagePartPath = packageModel.CreateEntry(packageEntryKey, packageEntry);
                    var packagePart = package.CreatePart(new(packagePartPath, UriKind.Relative), "application/json", default);

                    using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        await JsonSerializer.SerializeAsync(packagePartStream, documentNodes, JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        _logger.LogInformation("Created {FilePath}", packagePath);

        return true;
    }

    public async Task<bool> DeployPackageAsync(string packagePath, CosmosCredential cosmosCredential, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(packagePath);
        ArgumentNullException.ThrowIfNull(cosmosCredential);

        using (var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _logger.LogInformation("Unpacking {FilePath}", packagePath);

            var packageEntries = default(IReadOnlyDictionary<Guid, PackageEntry>);
            var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

            await using (packageModel.ConfigureAwait(false))
            {
                packageEntries = packageModel.ExtractEntries();
            }

            var applicationVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            var cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationName = $"cotopaxi/{applicationVersion}",
                UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
                EnableContentResponseOnWrite = false,
            };

            using var cosmosClient = cosmosCredential.IsConnectionString ?
                new CosmosClient(cosmosCredential.ConnectionString, cosmosClientOptions) :
                new CosmosClient(cosmosCredential.AccountEndpoint, cosmosCredential.AuthKeyOrResourceToken, cosmosClientOptions);

            _logger.LogInformation("Deploying the package to {CosmosEndpoint}", cosmosClient.Endpoint.OriginalString.TrimEnd('/'));

            var deployCharge = 0.0;

            var packageEntryGroupsByDatabase = packageEntries
                .GroupBy(static x => x.Value.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var packageEntryGroupByDatabase in packageEntryGroupsByDatabase)
            {
                var packageEntryGroupsByContainer = packageEntryGroupByDatabase
                    .GroupBy(static x => x.Value.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var packageEntryGroupByContainer in packageEntryGroupsByContainer)
                {
                    var container = cosmosClient.GetContainer(packageEntryGroupByDatabase.Key, packageEntryGroupByContainer.Key);
                    var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    var containerPartitionKeyPaths = containerResponse.Resource.PartitionKeyPaths.Select(static x => new JsonPointer(x)).ToArray();

                    deployCharge += containerResponse.RequestCharge;

                    _logger.LogInformation(
                        "Acquiring partition key paths for {DatabaseName}\\{ContainerName} - OK (HTTP {StatusCode}, {RU} RU)",
                        packageEntryGroupByDatabase.Key,
                        packageEntryGroupByContainer.Key,
                        (int)containerResponse.StatusCode,
                        Math.Round(containerResponse.RequestCharge, 2));

                    var packageEntryGroupsByOperation = packageEntryGroupByContainer
                        .GroupBy(static x => x.Value.OperationName, StringComparer.Ordinal)
                        .OrderBy(static x => x.Key, PackageOperationComparer.Instance);

                    foreach (var packageEntryGroupByOperation in packageEntryGroupsByOperation)
                    {
                        foreach (var (packageEntryKey, packageEntry) in packageEntryGroupByOperation)
                        {
                            var packagePart = package.GetPart(new(packageEntry.SourcePath, UriKind.Relative));
                            var documentNodes = default(JsonObject?[]);

                            using (var packageEntryStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
                            {
                                documentNodes = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packageEntryStream, JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            _logger.LogInformation(
                                "Deploying {PackageEntryUrn} to {DatabaseName}\\{ContainerName}",
                                GetPackageEntryUrn(packageEntryKey),
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
                                        "{OperationName} {PackageDocumentUrn} - ERROR (the document doesn't have proper partition key and ID)",
                                        packageEntry.OperationName,
                                        GetPackageDocumentUrn(packageEntryKey, i));

                                    _logger.LogError(
                                        "Deploying the package to {CosmosEndpoint} - ABORTED ({RU} RU)",
                                        cosmosClient.Endpoint.OriginalString.TrimEnd('/'),
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
                                            "{OperationName} {PackageDocumentUrn} - OK (HTTP {StatusCode}, {RU} RU)",
                                            packageEntry.OperationName,
                                            GetPackageDocumentUrn(packageEntryKey, i),
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
                                            "{OperationName} {PackageDocumentUrn} - OK (HTTP {StatusCode}, {RU} RU)",
                                            packageEntry.OperationName,
                                            GetPackageDocumentUrn(packageEntryKey, i),
                                            (int)ex.StatusCode,
                                            Math.Round(ex.RequestCharge, 2));
                                    }
                                    else
                                    {
                                        _logger.LogError(
                                            "{OperationName} {PackageDocumentUrn} - ERROR (HTTP {StatusCode}, {RU} RU, Activity {ActivityID})",
                                            packageEntry.OperationName,
                                            GetPackageDocumentUrn(packageEntryKey, i),
                                            (int)ex.StatusCode,
                                            Math.Round(ex.RequestCharge, 2),
                                            ex.ActivityId);

                                        _logger.LogError(
                                            "Deploying the package to {CosmosEndpoint} - ABORTED ({RU} RU)",
                                            cosmosClient.Endpoint.OriginalString.TrimEnd('/'),
                                            Math.Round(deployCharge, 2));

                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Deploying the package to {CosmosEndpoint} - DONE ({RU} RU)", cosmosClient.Endpoint.OriginalString.TrimEnd('/'), Math.Round(deployCharge, 2));
        }

        return true;
    }

    private static async Task<List<PackageEntry>> CollectPackageEntriesAsync(string projectPath, CancellationToken cancellationToken)
    {
        var packageEntries = new List<PackageEntry>();
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var projectNode = default(ProjectNode);

        using (var projectStream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            projectNode = await JsonSerializer.DeserializeAsync<ProjectNode>(projectStream, s_readJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        if (projectNode is not null)
        {
            foreach (var projectDatabaseNode in projectNode.Databases.Where(static x => x is not null))
            {
                if (!CosmosResource.IsProperDatabaseName(projectDatabaseNode!.Name))
                {
                    throw new JsonException("A project database node has errors");
                }

                foreach (var projectContainerNode in projectDatabaseNode.Containers.Where(static x => x is not null))
                {
                    if (!CosmosResource.IsProperContainerName(projectContainerNode!.Name))
                    {
                        throw new JsonException("An project container node has errors");
                    }

                    foreach (var projectOperationNode in projectContainerNode.Operations.Where(static x => x is not null))
                    {
                        if (projectOperationNode!.Name is not { Length: > 0 })
                        {
                            throw new JsonException("A project operation node has errors");
                        }

                        packageEntries.EnsureCapacity(packageEntries.Count + projectOperationNode.Sources.Length);

                        foreach (var source in projectOperationNode.Sources.Distinct(StringComparer.Ordinal))
                        {
                            if (source is not { Length: > 0 })
                            {
                                throw new JsonException("A project operation node has errors");
                            }

                            var packageEntry = new PackageEntry(
                                projectDatabaseNode.Name,
                                projectContainerNode.Name,
                                projectOperationNode.Name.ToUpperInvariant(),
                                Path.GetFullPath(source, projectDirectory));

                            packageEntries.Add(packageEntry);
                        }
                    }
                }
            }
        }

        return packageEntries;
    }

    private static string GetPackageEntryUrn(Guid packageEntryID)
    {
        return FormattableString.Invariant($"urn:cdbpkg:{packageEntryID}");
    }

    private static string GetPackageDocumentUrn(Guid packageEntryID, int documentIndex)
    {
        return FormattableString.Invariant($"urn:cdbpkg:{packageEntryID}:{documentIndex}");
    }
}
