// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class CosmosResourceNameConverter : JsonConverter<CosmosResourceName>
{
    public override CosmosResourceName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, CosmosResourceName value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
