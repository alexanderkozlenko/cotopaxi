// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

public sealed class PackagePartition
{
    public PackagePartition(
        Uri partitionUri,
        string partitionName,
        string databaseName,
        string containerName,
        PackageOperationType operationType)
    {
        ArgumentNullException.ThrowIfNull(partitionUri);
        ArgumentException.ThrowIfNullOrEmpty(partitionName);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);

        PartitionUri = partitionUri;
        PartitionName = partitionName;
        DatabaseName = databaseName;
        ContainerName = containerName;
        OperationType = operationType;
    }

    public Uri PartitionUri
    {
        get;
    }

    public string PartitionName
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
