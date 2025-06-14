// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> ShowPackageInfoAsync(string packagePath, CancellationToken cancellationToken)
    {
        Debug.Assert(packagePath is not null);

        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var package = await DatabasePackage.OpenAsync(packageStream, FileMode.Open, FileAccess.Read, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("cdbpkg:properties:uuid: {Value}",
            package.PackageProperties.Identifier ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:version: {Value}",
            package.PackageProperties.Version ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:subject: {Value}",
            package.PackageProperties.Subject ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:created: {Value}",
            package.PackageProperties.Created?.ToUniversalTime().ToString("O") ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:creator: {Value}",
            package.PackageProperties.Creator ?? string.Empty);

        var packagePartitions = package.GetPartitions();

        foreach (var packagePartition in packagePartitions)
        {
            var documents = default(JsonObject?[]);

            await using (var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartitionStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];

                if (document is null)
                {
                    continue;
                }

                CosmosDocument.TryGetId(document, out var documentId);

                _logger.LogInformation(
                    "cdbpkg:{PartitionUri}:$[{DocumentIndex}]: {OperationName} /{DatabaseName}/{ContainerName}/{DocumentId} ({PropertyCount})",
                    packagePartition.Uri,
                    i,
                    packagePartition.OperationType.ToString().ToLowerInvariant(),
                    packagePartition.DatabaseName,
                    packagePartition.ContainerName,
                    documentId ?? "?",
                    document.Count);
            }
        }

        return true;
    }
}
