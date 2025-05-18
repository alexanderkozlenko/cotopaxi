// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

internal static class PackageOperation
{
    public static PackageOperationType Parse(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "delete" => PackageOperationType.Delete,
            "create" => PackageOperationType.Create,
            "upsert" => PackageOperationType.Upsert,
            "patch" => PackageOperationType.Patch,
            _ => throw new NotSupportedException($"The package operation '{value}' is not supported"),
        };
    }

    public static string Format(PackageOperationType value)
    {
        return value switch
        {
            PackageOperationType.Delete => "delete",
            PackageOperationType.Create => "create",
            PackageOperationType.Upsert => "upsert",
            PackageOperationType.Patch => "patch",
            _ => throw new InvalidOperationException(),
        };
    }
}
