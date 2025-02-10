// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

public static class CosmosDocument
{
    public static bool TryGetPartitionKey(JsonObject documentNode, IEnumerable<JsonPointer> partitionKeyPaths, out PartitionKey value)
    {
        Debug.Assert(documentNode is not null);
        Debug.Assert(partitionKeyPaths is not null);

        var builder = new PartitionKeyBuilder();

        foreach (var partitionKeyPath in partitionKeyPaths)
        {
            if (documentNode.TryGetNode(partitionKeyPath, out var partitionKeyNode))
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

    public static bool TryGetUniqueID(JsonObject documentNode, [NotNullWhen(true)] out string? value)
    {
        Debug.Assert(documentNode is not null);

        if (documentNode.TryGetPropertyValue("id", out var propertyValueNode))
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
}
