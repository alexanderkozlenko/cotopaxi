// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly record struct ComparisonStatistics
(
    int Created,
    int Updated,
    int Deleted
);
