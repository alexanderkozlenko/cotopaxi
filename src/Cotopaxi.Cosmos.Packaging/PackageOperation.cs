// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.Packaging;

public static class PackageOperation
{
    public static bool TryParse(string? source, [NotNullWhen(true)] out PackageOperationType value)
    {
        if (string.Equals(source, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            value = PackageOperationType.Delete;

            return true;
        }

        if (string.Equals(source, "CREATE", StringComparison.OrdinalIgnoreCase))
        {
            value = PackageOperationType.Create;

            return true;
        }

        if (string.Equals(source, "UPSERT", StringComparison.OrdinalIgnoreCase))
        {
            value = PackageOperationType.Upsert;

            return true;
        }

        if (string.Equals(source, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            value = PackageOperationType.Patch;

            return true;
        }

        value = default;

        return false;
    }

    public static string Format(PackageOperationType value)
    {
        return value switch
        {
            PackageOperationType.Delete => "delete",
            PackageOperationType.Create => "create",
            PackageOperationType.Upsert => "upsert",
            PackageOperationType.Patch => "patch",
            _ => throw new NotSupportedException(),
        };
    }
}
