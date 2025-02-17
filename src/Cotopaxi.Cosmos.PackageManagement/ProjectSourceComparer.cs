// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class ProjectSourceComparer : IEqualityComparer<ProjectSource?>
{
    public static readonly IEqualityComparer<ProjectSource?> Instance = new ProjectSourceComparer();

    public bool Equals(ProjectSource? x, ProjectSource? y)
    {
        return
            (x?.OperationType == y?.OperationType) &&
            string.Equals(x?.DatabaseName, y?.DatabaseName, StringComparison.Ordinal) &&
            string.Equals(x?.ContainerName, y?.ContainerName, StringComparison.Ordinal) &&
            string.Equals(x?.FilePath, y?.FilePath, StringComparison.OrdinalIgnoreCase);

    }

    public int GetHashCode([DisallowNull] ProjectSource? obj)
    {
        Debug.Assert(obj is not null);

        return HashCode.Combine(
            obj.DatabaseName,
            obj.ContainerName,
            obj.OperationType,
            obj.FilePath.ToUpperInvariant());
    }
}
