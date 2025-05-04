// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;
using Cotopaxi.Cosmos.Packaging;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> ShowPackageInfoAsync(string packagePath, CancellationToken cancellationToken)
    {
        Debug.Assert(packagePath is not null);

        using var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        _logger.LogInformation(
            "cdbpkg {PackageIdentifier} {PackageTimestamp} {PackageSummary}",
            package.PackageProperties.Identifier,
            package.PackageProperties.Created?.ToUniversalTime().ToString("O"),
            package.PackageProperties.Version ?? package.PackageProperties.Subject);

        var packagePartitions = default(IReadOnlyDictionary<Uri, PackagePartition>);

        using (var packageModel = await PackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false))
        {
            packagePartitions = packageModel.GetPartitions();
        }

        foreach (var (packagePartitionUri, packagePartition) in packagePartitions)
        {
            var packagePartitionSize = 0;
            var packagePart = package.GetPart(packagePartitionUri);

            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
            {
                var jsonDocumentOptions = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                };

                using (var packagePartDocument = await JsonDocument.ParseAsync(packagePartStream, jsonDocumentOptions, cancellationToken).ConfigureAwait(false))
                {
                    packagePartitionSize = packagePartDocument.RootElement.GetArrayLength();
                }
            }

            _logger.LogInformation(
                "cdbpkg:{PartitionKey}: {DatabaseName}\\{ContainerName} {OperationName} [{PartitionSize}]",
                packagePartition.PartitionKey,
                packagePartition.DatabaseName,
                packagePartition.ContainerName,
                packagePartition.OperationType.ToString().ToLowerInvariant(),
                packagePartitionSize);
        }

        return true;
    }
}
