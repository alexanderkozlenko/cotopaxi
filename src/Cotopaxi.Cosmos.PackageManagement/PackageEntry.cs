// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed record class PackageEntry
(
    string DatabaseName,
    string ContainerName,
    string OperationName,
    string SourcePath
);
