// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
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

        using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        _logger.LogInformation("cdbpkg:properties:uuid: {PackageUUID}",
            package.PackageProperties.Identifier ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:created: {PackageCreated}",
            package.PackageProperties.Created?.ToUniversalTime().ToString("O") ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:version: {PackageVersion}",
            package.PackageProperties.Version ?? string.Empty);

        _logger.LogInformation("cdbpkg:properties:subject: {PackageSubject}",
            package.PackageProperties.Subject ?? string.Empty);

        var packagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

        using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
        {
            packagePartitions = packageModel.GetPartitions();
        }

        foreach (var (packagePartitionUri, packagePartition) in packagePartitions)
        {
            var packagePart = package.GetPart(packagePartitionUri);
            var documents = default(JsonObject?[]);

            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(packagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
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
                    packagePartitionUri,
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
