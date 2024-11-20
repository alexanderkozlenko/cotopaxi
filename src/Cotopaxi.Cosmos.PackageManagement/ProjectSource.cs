// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed record class ProjectSource
(
    string FilePath,
    string DatabaseName,
    string ContainerName,
    string OperationName
);
