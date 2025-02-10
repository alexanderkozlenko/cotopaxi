// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class CosmosOperationComparer : IComparer<string?>
{
    public static readonly IComparer<string?> Instance = new CosmosOperationComparer();

    public int Compare(string? x, string? y)
    {
        var rankX = ComputeRank(x);
        var rankY = ComputeRank(y);

        return rankX.CompareTo(rankY);
    }

    private static int ComputeRank(string? value)
    {
        if (string.Equals(value, CosmosOperation.Delete, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(value, CosmosOperation.Create, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(value, CosmosOperation.Upsert, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (string.Equals(value, CosmosOperation.Patch, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 4;
    }
}
