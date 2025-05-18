// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

public sealed class PackagePartition
{
    public PackagePartition(string databaseName, string containerName, PackageOperationType operationType)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);

        DatabaseName = databaseName;
        ContainerName = containerName;
        OperationType = operationType;
    }

    public string DatabaseName
    {
        get;
    }

    public string ContainerName
    {
        get;
    }

    public PackageOperationType OperationType
    {
        get;
    }
}
