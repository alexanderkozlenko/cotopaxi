// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Cotopaxi.Cosmos.PackageManagement;

internal static class JsonNodeExtensions
{
    public static bool TryGetValue(this JsonNode root, JsonPointer pointer, out JsonNode? result)
    {
        Debug.Assert(root is not null);

        if (pointer.Tokens.IsEmpty)
        {
            result = root;

            return true;
        }

        var current = root;

        for (var i = 0; i < pointer.Tokens.Length; i++)
        {
            if (current is JsonObject jsonObject)
            {
                if (jsonObject.TryGetPropertyValue(pointer.Tokens[i], out current))
                {
                    continue;
                }
            }
            else if (current is JsonArray jsonArray)
            {
                if (int.TryParse(pointer.Tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    if (index < jsonArray.Count)
                    {
                        current = jsonArray[index];

                        continue;
                    }
                }
            }

            result = default;

            return false;
        }

        result = current;

        return true;
    }
}
