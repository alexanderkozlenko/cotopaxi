// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

public sealed class PackagePartition
{
    public PackagePartition(
        Guid partitionKey,
        string databaseName,
        string containerName,
        PackageOperationType operationType)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);

        PartitionKey = partitionKey;
        DatabaseName = databaseName;
        ContainerName = containerName;
        OperationType = operationType;
    }

    public Guid PartitionKey
    {
        get;
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
