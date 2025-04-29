// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class PartitionKeyConverter : JsonConverter<PartitionKey>
{
    public override PartitionKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(ref reader) is JsonArray node)
        {
            if (CosmosResource.TryGetPartitionKey(node, out var value))
            {
                return value;
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, PartitionKey value, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(value.ToString())!;

        node.WriteTo(writer, options);
    }
}
