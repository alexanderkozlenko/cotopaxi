// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed record class PackagePartition
(
    Uri PartitionUri,
    string PartitionName,
    string DatabaseName,
    string ContainerName,
    CosmosOperationType OperationType
);
