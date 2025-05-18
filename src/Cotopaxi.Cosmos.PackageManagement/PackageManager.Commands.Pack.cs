// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
using Cotopaxi.Cosmos.PackageManagement.Primitives;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> CreatePackageAsync(string projectPath, string packagePath, string? packageVersion, CancellationToken cancellationToken)
    {
        Debug.Assert(projectPath is not null);
        Debug.Assert(packagePath is not null);

        var projectVariables = new Dictionary<string, string?>
        {
            ["Version"] = packageVersion,
        };

        var projectSources = await ListProjectSourcesAsync(projectPath, projectVariables.ToFrozenDictionary(), cancellationToken).ConfigureAwait(false);
        var packageDirectory = Path.GetDirectoryName(packagePath);

        if (packageDirectory is not null)
        {
            Directory.CreateDirectory(packageDirectory);
        }

        try
        {
            using var package = Package.Open(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
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
                        .GroupBy(static x => x.OperationType)
                        .OrderBy(static x => x.Key);

                    foreach (var projectSourceGroupByOperations in projectSourceGroupsByOperations)
                    {
                        var packagePartitionKeySource = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}:{1}:{2}",
                            projectSourceGroupByDatabase.Key,
                            projectSourceGroupByContainer.Key,
                            projectSourceGroupByOperations.Key);

                        var packagePartitionKey = Uuid.CreateVersion8(packagePartitionKeySource);

                        var packagePartition = new PackagePartition(
                            packagePartitionKey,
                            projectSourceGroupByDatabase.Key,
                            projectSourceGroupByContainer.Key,
                            projectSourceGroupByOperations.Key);

                        var packagePartitionOperationName = packagePartition.OperationType.ToString().ToLowerInvariant();
                        var packagePartitionUri = packageModel.CreatePartition(packagePartition);

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

                                CosmosDocument.Prune(document);

                                if (!CosmosDocument.TryGetId(document, out var documentId))
                                {
                                    throw new InvalidOperationException($"Failed to extract document identifier from {projectSource.FilePath}:$[{i}]");
                                }

                                if (!CosmosDocumentId.IsWellFormed(documentId))
                                {
                                    throw new InvalidOperationException($"A malformed document identifier in {projectSource.FilePath}:$[{i}]");
                                }

                                if (documentsByOperation.Any(x => JsonNode.DeepEquals(x, document)))
                                {
                                    throw new InvalidOperationException($"A duplicate document+operation entry {projectSource.FilePath}:$[{i}]");
                                }

                                documentsByOperation.Add(document);

                                _logger.LogInformation(
                                    "{SourcePath}:$[{DocumentIndex}]: {OperationName} {DatabaseName}\\{ContainerName}\\{DocumentId} ({PropertyCount})",
                                    projectSource.FilePath,
                                    i,
                                    packagePartitionOperationName,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName,
                                    documentId,
                                    document.Count);
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

            await packageModel.SaveAsync(cancellationToken).ConfigureAwait(false);

            package.PackageProperties.Identifier = Guid.CreateVersion7().ToString();
            package.PackageProperties.Version = packageVersion;
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

    private static async Task<FrozenSet<ProjectSource>> ListProjectSourcesAsync(string projectPath, FrozenDictionary<string, string?> projectVariables, CancellationToken cancellationToken)
    {
        var projectSources = new HashSet<ProjectSource>(ProjectSourceComparer.Instance);
        var projectNode = default(ProjectNode);

        using (var projectStream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            projectNode = await JsonSerializer.DeserializeAsync<ProjectNode>(projectStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        if (projectNode is not null)
        {
            var projectSearchPath = Path.GetDirectoryName(projectPath)!;

            foreach (var projectDatabaseNode in projectNode.Databases.Where(static x => x is not null))
            {
                foreach (var projectContainerNode in projectDatabaseNode!.Containers.Where(static x => x is not null))
                {
                    foreach (var projectOperationNode in projectContainerNode!.Operations.Where(static x => x is not null))
                    {
                        foreach (var projectSourcePatternValue in projectOperationNode!.Documents.Where(static x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            var projectSourcePattern = projectSourcePatternValue!;

                            foreach (var (variableName, variableValue) in projectVariables)
                            {
                                if (!string.IsNullOrEmpty(variableValue))
                                {
                                    projectSourcePattern = projectSourcePattern.Replace($"$({variableName})", variableValue, StringComparison.OrdinalIgnoreCase);
                                }
                            }

                            var projectSourcePaths = PathGlobbing.GetFilePaths(projectSourcePattern!, projectSearchPath);

                            foreach (var projectSourcePath in projectSourcePaths)
                            {
                                var projectSource = new ProjectSource(
                                    projectSourcePath,
                                    projectDatabaseNode.Name.Value,
                                    projectContainerNode.Name.Value,
                                    projectOperationNode.OperationType);

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
