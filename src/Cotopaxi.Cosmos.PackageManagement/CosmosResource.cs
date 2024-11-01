// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

internal static class CosmosResource
{
    public static bool IsProperDatabaseName(string? value)
    {
        return value is { Length: > 0 and <= 256 };
    }

    public static bool IsProperContainerName(string? value)
    {
        return value is { Length: > 0 and <= 256 };
    }

    public static bool IsProperUniqueID(string? value)
    {
        return value is { Length: > 0 and < 256 };
    }

    public static bool TryGetPartitionKey(JsonObject documentNode, ReadOnlySpan<JsonPointer> partitionKeyPaths, out PartitionKey result)
    {
        var builder = new PartitionKeyBuilder();

        foreach (var partitionKeyPath in partitionKeyPaths)
        {
            if (documentNode.TryGetValue(partitionKeyPath, out var partitionKeyNode))
            {
                if (partitionKeyNode is not null)
                {
                    switch (partitionKeyNode.GetValueKind())
                    {
                        case JsonValueKind.String:
                            {
                                builder.Add(partitionKeyNode.GetValue<string>());
                            }
                            break;
                        case JsonValueKind.Number:
                            {
                                builder.Add(partitionKeyNode.GetValue<double>());
                            }
                            break;
                        case JsonValueKind.True:
                            {
                                builder.Add(true);
                            }
                            break;
                        case JsonValueKind.False:
                            {
                                builder.Add(false);
                            }
                            break;
                        default:
                            {
                                result = default;

                                return false;
                            }
                    }
                }
                else
                {
                    builder.AddNullValue();
                }
            }
            else
            {
                builder.AddNoneType();
            }
        }

        result = builder.Build();

        return true;
    }

    public static bool TryGetUniqueID(JsonObject documentNode, [NotNullWhen(true)] out string? result)
    {
        if (documentNode.TryGetPropertyValue("id", out var valueNode) && (valueNode is JsonValue jsonValue) && jsonValue.TryGetValue(out result))
        {
            return true;
        }

        result = default;

        return false;
    }

    public static void RemoveSystemProperties(JsonObject documentNode)
    {
        documentNode.Remove("_attachments");
        documentNode.Remove("_etag");
        documentNode.Remove("_rid");
        documentNode.Remove("_self");
        documentNode.Remove("_ts");
    }
}
