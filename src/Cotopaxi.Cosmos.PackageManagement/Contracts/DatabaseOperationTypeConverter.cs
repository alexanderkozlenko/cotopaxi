// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Cotopaxi.Cosmos.Packaging;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class DatabaseOperationTypeConverter : JsonConverter<DatabaseOperationType>
{
    public override DatabaseOperationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString()?.ToLowerInvariant() switch
        {
            "delete" => DatabaseOperationType.Delete,
            "create" => DatabaseOperationType.Create,
            "upsert" => DatabaseOperationType.Upsert,
            "patch" => DatabaseOperationType.Patch,
            _ => throw new JsonException(),
        };
    }

    public override void Write(Utf8JsonWriter writer, DatabaseOperationType value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}
