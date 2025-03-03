// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

public static class CosmosResource
{
    private static readonly char[] s_invalidChars = ['/', '\\', '#', '?'];

    public static bool TryGetPartitionKey(JsonObject document, IEnumerable<JsonPointer> partitionKeyPaths, out PartitionKey value)
    {
        Debug.Assert(document is not null);
        Debug.Assert(partitionKeyPaths is not null);

        var builder = new PartitionKeyBuilder();

        foreach (var partitionKeyPath in partitionKeyPaths)
        {
            if (document.TryGetNode(partitionKeyPath, out var partitionKeyNode))
            {
                if (partitionKeyNode is not null)
                {
                    var valueKind = partitionKeyNode.GetValueKind();

                    if (valueKind == JsonValueKind.String)
                    {
                        builder.Add(partitionKeyNode.GetValue<string>());
                    }
                    else if (valueKind == JsonValueKind.Number)
                    {
                        builder.Add(partitionKeyNode.GetValue<double>());
                    }
                    else if (valueKind == JsonValueKind.True)
                    {
                        builder.Add(true);
                    }
                    else if (valueKind == JsonValueKind.False)
                    {
                        builder.Add(false);
                    }
                    else
                    {
                        value = default;

                        return false;
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

        value = builder.Build();

        return true;
    }

    public static bool TryGetDocumentID(JsonObject document, [NotNullWhen(true)] out string? value)
    {
        Debug.Assert(document is not null);

        if (document.TryGetPropertyValue("id", out var propertyValueNode))
        {
            if (propertyValueNode is JsonValue valueNode)
            {
                if (valueNode.TryGetValue(out value))
                {
                    return true;
                }
            }
        }

        value = default;

        return false;
    }

    public static void RemoveSystemProperties(JsonObject document)
    {
        Debug.Assert(document is not null);

        document.Remove("_attachments");
        document.Remove("_etag");
        document.Remove("_rid");
        document.Remove("_self");
        document.Remove("_ts");
    }

    public static bool IsValidResourceID(string? value)
    {
        if (value is not { Length: > 0 and < 256 })
        {
            return false;
        }

        if (value.IndexOfAny(s_invalidChars) >= 0)
        {
            return false;
        }

        return true;
    }
}
