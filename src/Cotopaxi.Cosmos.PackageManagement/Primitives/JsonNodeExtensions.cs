// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace System.Text.Json.Nodes;

public static class JsonNodeExtensions
{
    public static bool TryGetNode(this JsonNode root, JsonPointer path, out JsonNode? node)
    {
        Debug.Assert(root is not null);

        var current = root;

        for (var i = 0; i < path.Tokens.Length; i++)
        {
            if (current is JsonObject currentObject)
            {
                if (currentObject.TryGetPropertyValue(path.Tokens[i], out current))
                {
                    continue;
                }
            }
            else if (current is JsonArray currentArray)
            {
                if (int.TryParse(path.Tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    if (index < currentArray.Count)
                    {
                        current = currentArray[index];

                        continue;
                    }
                }
            }

            node = default;

            return false;
        }

        node = current;

        return true;
    }
}
