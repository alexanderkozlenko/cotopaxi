// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly record struct OperationStatistics
(
    int Created,
    int Updated,
    int Deleted
);
