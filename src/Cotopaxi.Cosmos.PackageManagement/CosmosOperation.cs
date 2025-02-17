// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement;

public static class CosmosOperation
{
    public static bool TryParse(string? source, [NotNullWhen(true)] out CosmosOperationType value)
    {
        if (string.Equals(source, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            value = CosmosOperationType.Delete;

            return true;
        }

        if (string.Equals(source, "CREATE", StringComparison.OrdinalIgnoreCase))
        {
            value = CosmosOperationType.Create;

            return true;
        }

        if (string.Equals(source, "UPSERT", StringComparison.OrdinalIgnoreCase))
        {
            value = CosmosOperationType.Upsert;

            return true;
        }

        if (string.Equals(source, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            value = CosmosOperationType.Patch;

            return true;
        }

        value = default;

        return false;
    }

    public static string Format(CosmosOperationType value)
    {
        return value switch
        {
            CosmosOperationType.Delete => "delete",
            CosmosOperationType.Create => "create",
            CosmosOperationType.Upsert => "upsert",
            CosmosOperationType.Patch => "patch",
            _ => throw new NotSupportedException(),
        };
    }
}
