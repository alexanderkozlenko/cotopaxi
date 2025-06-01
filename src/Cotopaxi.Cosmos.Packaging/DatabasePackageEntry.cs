// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

internal sealed record class DatabasePackageEntry
(
    Uri PartitionUri,
    string DatabaseName,
    string ContainerName,
    DatabaseOperationType OperationType
);
