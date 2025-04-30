// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

public static class CosmosDocument
{
    public static bool TryGetPartitionKey(JsonArray source, out PartitionKey value)
    {
        Debug.Assert(source is not null);

        var builder = new PartitionKeyBuilder();

        foreach (var partitionKeyNode in source)
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
                else if (valueKind == JsonValueKind.Object)
                {
                    if (((JsonObject)partitionKeyNode).Count == 0)
                    {
                        builder.AddNoneType();
                    }
                    else
                    {
                        value = default;

                        return false;
                    }
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

        value = builder.Build();

        return true;
    }

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

    public static bool TryGetId(JsonObject document, [NotNullWhen(true)] out string? value)
    {
        Debug.Assert(document is not null);

        if (document.TryGetPropertyValue("id", out var idNode))
        {
            if (idNode is JsonValue valueNode)
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

    public static void Prune(JsonObject document)
    {
        Debug.Assert(document is not null);

        document.Remove("_attachments");
        document.Remove("_etag");
        document.Remove("_rid");
        document.Remove("_self");
        document.Remove("_ts");
    }

    public static void Format(JsonObject document)
    {
        Debug.Assert(document is not null);

        Prune(document);

        if (document.TryGetPropertyValue("id", out var idNode))
        {
            document.Remove("id");
            document.Insert(0, "id", idNode);
        }
    }
}
