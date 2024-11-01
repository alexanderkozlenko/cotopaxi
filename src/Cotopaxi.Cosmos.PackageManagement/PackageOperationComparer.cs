// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class PackageOperationComparer : IComparer<string?>
{
    public static readonly IComparer<string?> Instance = new PackageOperationComparer();

    public int Compare(string? x, string? y)
    {
        var rankX = ComputeRank(x);
        var rankY = ComputeRank(y);

        return rankX.CompareTo(rankY);
    }

    private static int ComputeRank(ReadOnlySpan<char> value)
    {
        return value switch
        {
            "DELETE" => 0,
            "CREATE" => 1,
            "UPSERT" => 2,
            _ => 3,
        };
    }
}
