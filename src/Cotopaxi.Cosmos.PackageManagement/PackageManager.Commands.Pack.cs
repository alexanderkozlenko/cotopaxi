﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
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

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

        try
        {
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

                                _logger.LogInformation(
                                    "Packing {SourcePath}:$[{DocumentIndex}] for {OperationName} in {DatabaseName}\\{ContainerName}",
                                    projectSource.FilePath,
                                    i,
                                    packagePartitionOperationName,
                                    packagePartition.DatabaseName,
                                    packagePartition.ContainerName);

                                if (!CosmosResource.TryGetDocumentId(document, out var documentId) || !CosmosResource.IsSupportedResourceId(documentId))
                                {
                                    throw new InvalidOperationException($"Unable to get document identifier for {projectSource.FilePath}:$[{i}]");
                                }

                                CosmosResource.CleanupDocument(document);

                                if (documentsByOperation.Any(x => JsonNode.DeepEquals(x, document)))
                                {
                                    throw new InvalidOperationException($"Unable to include duplicate entry {projectSource.FilePath}:$[{i}]");
                                }

                                documentsByOperation.Add(document);
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
            package.PackageProperties.Created = DateTime.UtcNow;
        }
        catch
        {
            File.Delete(packagePath);

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
                if (!CosmosResource.IsSupportedResourceId(projectDatabaseNode!.Name))
                {
                    throw new JsonException($"JSON deserialization for type '{typeof(ProjectDatabaseNode)}' encountered errors");
                }

                foreach (var projectContainerNode in projectDatabaseNode.Containers.Where(static x => x is not null))
                {
                    if (!CosmosResource.IsSupportedResourceId(projectContainerNode!.Name))
                    {
                        throw new JsonException($"JSON deserialization for type '{typeof(ProjectContainerNode)}' encountered errors");
                    }

                    foreach (var projectOperationNode in projectContainerNode.Operations.Where(static x => x is not null))
                    {
                        var packageOperationType = projectOperationNode!.Name?.ToLowerInvariant() switch
                        {
                            "delete" => PackageOperationType.Delete,
                            "create" => PackageOperationType.Create,
                            "upsert" => PackageOperationType.Upsert,
                            "patch" => PackageOperationType.Patch,
                            _ => throw new JsonException($"JSON deserialization for type '{typeof(ProjectOperationNode)}' encountered errors"),
                        };

                        foreach (var projectSourcePatternValue in projectOperationNode.Documents.Where(static x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            var projectSourcePattern = projectSourcePatternValue!;

                            foreach (var (variableName, variableValue) in projectVariables)
                            {
                                if (!string.IsNullOrEmpty(variableValue))
                                {
                                    projectSourcePattern = projectSourcePattern.Replace($"$({variableName})", variableValue, StringComparison.OrdinalIgnoreCase);
                                }
                            }

                            var projectSourcePaths = GetFiles(projectSearchPath, projectSourcePattern!);

                            foreach (var projectSourcePath in projectSourcePaths)
                            {
                                var projectSource = new ProjectSource(
                                    projectSourcePath,
                                    projectDatabaseNode.Name,
                                    projectContainerNode.Name,
                                    packageOperationType);

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
