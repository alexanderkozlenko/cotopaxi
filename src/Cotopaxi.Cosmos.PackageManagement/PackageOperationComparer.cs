// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class PackageOperationComparer : IComparer<string?>
{
    public static readonly IComparer<string?> Instance = new PackageOperationComparer();

    public int Compare(string? x, string? y)
    {
        var rankX = ComputeRank(x);
        var rankY = ComputeRank(y);

        return rankX.CompareTo(rankY);
    }

    private static int ComputeRank(string? value)
    {
        if (string.Equals(value, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(value, "CREATE", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(value, "UPSERT", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }
}
