// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public static class CosmosOperation
{
    public const string Delete = "DELETE";
    public const string Create = "CREATE";
    public const string Upsert = "UPSERT";
    public const string Patch = "PATCH";

    public static bool IsSupported(string? value)
    {
        return
            string.Equals(value, Upsert, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Create, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Patch, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Delete, StringComparison.OrdinalIgnoreCase);
    }
}
