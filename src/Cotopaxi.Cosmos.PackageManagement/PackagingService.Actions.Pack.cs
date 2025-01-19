// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackagingService
{
    public async Task<bool> CreatePackageAsync(string projectPath, string packagePath, string? packageVersion, CancellationToken cancellationToken)
    {
        Debug.Assert(projectPath is not null);
        Debug.Assert(packagePath is not null);

        _logger.LogInformation("Building package {PackagePath} for project {ProjectPath}", packagePath, projectPath);

        try
        {
            var projectSources = await ListProjectSourcesAsync(projectPath, cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

            using var package = Package.Open(packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

            var projectSourceGroupsByDatabase = projectSources
                .GroupBy(static x => x.DatabaseName, StringComparer.Ordinal)
                .OrderBy(static x => x.Key, StringComparer.Ordinal);

            foreach (var projectSourceGroupByDatabase in projectSourceGroupsByDatabase)
            {
                var projectSourceGroupsByContainer = projectSourceGroupByDatabase
                    .GroupBy(static x => x.ContainerName, StringComparer.Ordinal)
                    .OrderBy(static x => x.Key, StringComparer.Ordinal);

                foreach (var projectSourceGroupByContainer in projectSourceGroupsByContainer)
                {
                    var projectSourceGroupsByOperations = projectSourceGroupByContainer
                        .GroupBy(static x => x.OperationName, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(static x => x.Key, PackageOperationComparer.Instance);

                    foreach (var projectSourceGroupByOperations in projectSourceGroupsByOperations)
                    {
                        var packagePartitionKeySource = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}:{1}:{2}",
                            projectSourceGroupByDatabase.Key,
                            projectSourceGroupByContainer.Key,
                            projectSourceGroupByOperations.Key);

                        var packagePartitionKey = CreateUUID(packagePartitionKeySource);
                        var packagePartitionName = packagePartitionKey.ToString();

                        var packagePartitionUri = packageModel.CreatePartition(
                            packagePartitionName,
                            projectSourceGroupByDatabase.Key,
                            projectSourceGroupByContainer.Key,
                            projectSourceGroupByOperations.Key.ToLowerInvariant());

                        _logger.LogInformation(
                            "Packing document collection {PartitionName} as {OperationName} operations for {DatabaseName}\\{ContainerName}",
                            packagePartitionName,
                            projectSourceGroupByOperations.Key,
                            projectSourceGroupByDatabase.Key,
                            projectSourceGroupByContainer.Key);

                        var projectSourcesByOperation = projectSourceGroupByOperations
                            .OrderBy(static x => x.FilePath, StringComparer.OrdinalIgnoreCase);

                        var documentsByOperation = new List<JsonObject>();

                        foreach (var projectSource in projectSourcesByOperation)
                        {
                            var documentsBySource = default(JsonObject?[]);

                            using (var projectSourceStream = new FileStream(projectSource.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                documentsBySource = await JsonSerializer.DeserializeAsync<JsonObject?[]>(projectSourceStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                            }

                            documentsByOperation.EnsureCapacity(documentsByOperation.Count + documentsBySource.Length);

                            for (var i = 0; i < documentsBySource.Length; i++)
                            {
                                var document = documentsBySource[i];

                                if (document is null)
                                {
                                    continue;
                                }

                                if (!CosmosDocument.TryGetUniqueID(document, out var documentUID) || (documentUID is not { Length: > 0 and < 256 }))
                                {
                                    throw new InvalidOperationException($"Cannot get document identifier for {projectSource.FilePath}:$[{i}]");
                                }

                                document.Remove("_attachments");
                                document.Remove("_etag");
                                document.Remove("_rid");
                                document.Remove("_self");
                                document.Remove("_ts");

                                documentsByOperation.Add(document);

                                _logger.LogInformation("Packing document {SourcePath}:$[{DocumentIndex}]", projectSource.FilePath, i);
                            }
                        }

                        var packagePart = package.CreatePart(packagePartitionUri, "application/json", default);

                        using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            await JsonSerializer.SerializeAsync(packagePartStream, documentsByOperation, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await packageModel.SaveAsync().ConfigureAwait(false);

            package.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            package.PackageProperties.Version = packageVersion;
            package.PackageProperties.Created = DateTime.UtcNow;
        }
        catch
        {
            File.Delete(packagePath);

            throw;
        }

        return true;
    }

    private static async Task<FrozenSet<ProjectSource>> ListProjectSourcesAsync(string projectPath, CancellationToken cancellationToken)
    {
        var projectSources = new HashSet<ProjectSource>(ProjectSourceComparer.Instance);
        var projectNode = default(ProjectNode);

        using (var projectStream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            projectNode = await JsonSerializer.DeserializeAsync<ProjectNode>(projectStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        if (projectNode is not null)
        {
            foreach (var projectDatabaseNode in projectNode.Databases.Where(static x => x is not null))
            {
                if (projectDatabaseNode!.Name is not { Length: > 0 and <= 256 })
                {
                    throw new JsonException($"JSON deserialization for type '{typeof(ProjectDatabaseNode)}' encountered errors");
                }

                foreach (var projectContainerNode in projectDatabaseNode.Containers.Where(static x => x is not null))
                {
                    if (projectContainerNode!.Name is not { Length: > 0 and <= 256 })
                    {
                        throw new JsonException($"JSON deserialization for type '{typeof(ProjectContainerNode)}' encountered errors");
                    }

                    foreach (var projectOperationNode in projectContainerNode.Operations.Where(static x => x is not null))
                    {
                        if (projectOperationNode!.Name is not { Length: > 0 })
                        {
                            throw new JsonException($"JSON deserialization for type '{typeof(ProjectOperationNode)}' encountered errors");
                        }

                        foreach (var projectSourcePattern in projectOperationNode.Documents.Where(static x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            var projectSearchPath = Path.GetDirectoryName(projectPath)!;
                            var projectSourcePaths = GetFiles(projectSearchPath, projectSourcePattern!);

                            foreach (var projectSourcePath in projectSourcePaths)
                            {
                                var projectSource = new ProjectSource(
                                    projectSourcePath,
                                    projectDatabaseNode.Name,
                                    projectContainerNode.Name,
                                    projectOperationNode.Name.ToUpperInvariant());

                                projectSources.Add(projectSource);
                            }
                        }
                    }
                }
            }
        }

        return projectSources.ToFrozenSet(ProjectSourceComparer.Instance);
    }
}
