// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
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
            var packagePart = package.GetPart(packagePartitionUri);
            var documents = default(PackagePartitionItemNode[]);

            using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
            {
                documents = await JsonSerializer.DeserializeAsync<PackagePartitionItemNode[]>(packagePartStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            _logger.LogInformation(
                "cdbpkg:{PartitionKey}: {DatabaseName}\\{ContainerName} {OperationName} [{PartitionSize}]",
                packagePartition.PartitionKey,
                packagePartition.DatabaseName,
                packagePartition.ContainerName,
                packagePartition.OperationType.ToString().ToLowerInvariant(),
                documents.Length);
        }

        return true;
    }
}
