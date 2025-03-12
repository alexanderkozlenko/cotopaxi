// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using Cotopaxi.Cosmos.Packaging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed record class ProjectSource
(
    string FilePath,
    string DatabaseName,
    string ContainerName,
    PackageOperationType OperationType
);
