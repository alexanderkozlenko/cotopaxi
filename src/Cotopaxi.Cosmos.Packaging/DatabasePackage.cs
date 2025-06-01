// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.IO.Packaging;

namespace Cotopaxi.Cosmos.Packaging;

public sealed class DatabasePackage : IAsyncDisposable
{
    private readonly Package _package;
    private readonly DatabasePackageProperties _packageProperties;
    private readonly DatabasePackageModel _packageModel;

    private DatabasePackage(Package package, DatabasePackageModel packageModel)
    {
        _package = package;
        _packageProperties = new(package.PackageProperties);
        _packageModel = packageModel;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _packageModel.CloseAsync().ConfigureAwait(false);

        _packageModel.Dispose();
        _package.Close();
    }

    public static async Task<DatabasePackage> OpenAsync(Stream stream, FileMode packageMode, FileAccess packageAccess, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var package = Package.Open(stream, packageMode, packageAccess);
        var packageModel = await DatabasePackageModel.OpenAsync(package, default, cancellationToken).ConfigureAwait(false);

        return new(package, packageModel);
    }

    public DatabasePackagePartition CreatePartition(string databaseName, string containerName, DatabaseOperationType operationType)
    {
        ArgumentNullException.ThrowIfNull(databaseName);
        ArgumentNullException.ThrowIfNull(containerName);

        if (!_package.FileOpenAccess.HasFlag(FileAccess.ReadWrite))
        {
            throw new IOException("The package must be opened with read and write access");
        }

        var packageEntryKey = Guid.CreateVersion7();
        var packageEntry = _packageModel.CreatePackageEntry(packageEntryKey, databaseName, containerName, operationType);
        var packagePart = _package.CreatePart(packageEntry.PartitionUri, "application/json", default);

        return new(
            packagePart,
            packageEntry.DatabaseName,
            packageEntry.ContainerName,
            packageEntry.OperationType);
    }

    public DatabasePackagePartition[] GetPartitions()
    {
        if (!_package.FileOpenAccess.HasFlag(FileAccess.Read))
        {
            throw new IOException("The package must be opened with read access");
        }

        var packageEntries = _packageModel.GetPackageEntries();
        var packagePartitions = new DatabasePackagePartition[packageEntries.Length];

        for (var i = 0; i < packagePartitions.Length; i++)
        {
            var packageEntry = packageEntries[i];
            var packagePart = _package.GetPart(packageEntry.PartitionUri);

            packagePartitions[i] = new(
                packagePart,
                packageEntry.DatabaseName,
                packageEntry.ContainerName,
                packageEntry.OperationType);
        }

        return packagePartitions;
    }

    public DatabasePackageProperties PackageProperties
    {
        get
        {
            return _packageProperties;
        }
    }
}
