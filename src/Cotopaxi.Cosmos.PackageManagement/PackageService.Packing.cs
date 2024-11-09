// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.IO.Packaging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageService
{
    public async Task<bool> CreatePackageAsync(FileInfo projectFile, FileInfo packageFile, CancellationToken cancellationToken)
    {
        Debug.Assert(projectFile is not null);
        Debug.Assert(packageFile is not null);

        _logger.LogInformation("Reading project {FilePath}", projectFile);

        var packageEntries = await CollectPackageEntriesAsync(projectFile, cancellationToken).ConfigureAwait(false);

        packageFile.Directory!.Create();

        var packagingCompleted = false;

        try
        {
            using (var package = Package.Open(packageFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

                await using (packageModel.ConfigureAwait(false))
                {
                    foreach (var packageEntry in packageEntries.OrderBy(static x => x.SourcePath, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Reading document collection {FilePath}", packageEntry.SourcePath);

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

                        var packageEntryPath = packageModel.CreateEntry(packageEntry.UUID, packageEntry);

                        _logger.LogInformation(
                            "Packing cdbpkg:{PackageEntryPath} for {OperationName} in {DatabaseName}.{ContainerName}",
                            packageEntryPath,
                            packageEntry.OperationName,
                            packageEntry.DatabaseName,
                            packageEntry.ContainerName);

                        for (var i = 0; i < documentNodes.Length; i++)
                        {
                            var documentNode = documentNodes[i]!;

                            if (!CosmosResource.TryGetDocumentID(documentNode, out var documentID) || !CosmosResource.IsProperDocumentID(documentID))
                            {
                                _logger.LogError("Packing cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - ERROR (the document doesn't have a proper ID)", packageEntryPath, i);

                                return false;
                            }

                            CosmosResource.RemoveSystemProperties(documentNode);

                            _logger.LogInformation("Packing cdbpkg:{PackageEntryPath}:$[{DocumentIndex}] - OK", packageEntryPath, i);
                        }

                        var packagePart = package.CreatePart(new(packageEntryPath, UriKind.Relative), "application/json", default);

                        using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(packagePartStream, documentNodes, JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await packageModel.SaveAsync().ConfigureAwait(false);

                    packagingCompleted = true;
                }
            }
        }
        finally
        {
            if (packagingCompleted)
            {
                _logger.LogInformation("Successfully created package {FilePath}", packageFile.FullName);
            }
            else
            {
                _logger.LogError("Aborted creation of package {FilePath}", packageFile.FullName);

                packageFile.Delete();
            }
        }

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
                if (!CosmosResource.IsProperSystemName(projectDatabaseNode!.Name))
                {
                    throw new JsonException($"JSON deserialization for type '{typeof(ProjectDatabaseNode)}' encountered errors");
                }

                foreach (var projectContainerNode in projectDatabaseNode.Containers.Where(static x => x is not null))
                {
                    if (!CosmosResource.IsProperSystemName(projectContainerNode!.Name))
                    {
                        throw new JsonException($"JSON deserialization for type '{typeof(ProjectContainerNode)}' encountered errors");
                    }

                    foreach (var projectOperationNode in projectContainerNode.Operations.Where(static x => x is not null))
                    {
                        if (projectOperationNode!.Name is not { Length: > 0 })
                        {
                            throw new JsonException($"JSON deserialization for type '{typeof(ProjectOperationNode)}' encountered errors");
                        }

                        foreach (var collectionPathPattern in projectOperationNode.Documents.Where(static x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
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

        return match.Files
            .Select(x => Path.GetFullPath(Path.Combine(directory.FullName, x.Path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Guid GetPackageEntryUUID(string source)
    {
        // UUID Version 8 (RFC 9562) - XXH128

        var hash = XxHash128.Hash(Encoding.Unicode.GetBytes(source));

        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new(hash);
    }
}
