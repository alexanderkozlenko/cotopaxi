// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly record struct DifferenceCounters
(
    int Created,
    int Updated,
    int Deleted
);
