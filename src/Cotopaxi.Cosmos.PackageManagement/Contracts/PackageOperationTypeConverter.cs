// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Cotopaxi.Cosmos.Packaging;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class PackageOperationTypeConverter : JsonConverter<PackageOperationType>
{
    public override PackageOperationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString()?.ToLowerInvariant() switch
        {
            "delete" => PackageOperationType.Delete,
            "create" => PackageOperationType.Create,
            "upsert" => PackageOperationType.Upsert,
            "patch" => PackageOperationType.Patch,
            _ => throw new JsonException(),
        };
    }

    public override void Write(Utf8JsonWriter writer, PackageOperationType value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}
