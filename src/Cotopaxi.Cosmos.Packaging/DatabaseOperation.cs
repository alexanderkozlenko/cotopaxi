// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.Packaging;

internal static class DatabaseOperation
{
    public static DatabaseOperationType Parse(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "delete" => DatabaseOperationType.Delete,
            "create" => DatabaseOperationType.Create,
            "upsert" => DatabaseOperationType.Upsert,
            "patch" => DatabaseOperationType.Patch,
            _ => throw new NotSupportedException($"The package operation '{value}' is not supported"),
        };
    }

    public static string Format(DatabaseOperationType value)
    {
        return value switch
        {
            DatabaseOperationType.Delete => "delete",
            DatabaseOperationType.Create => "create",
            DatabaseOperationType.Upsert => "upsert",
            DatabaseOperationType.Patch => "patch",
            _ => throw new InvalidOperationException(),
        };
    }
}
