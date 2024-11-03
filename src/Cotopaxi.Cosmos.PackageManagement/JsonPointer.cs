// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly struct JsonPointer
{
    private readonly string[] _tokens;

    public JsonPointer(string value)
    {
        Debug.Assert(value is not null);

        var tokens = value.TrimStart('/').Split('/');

        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = tokens[i]
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
        }

        _tokens = tokens;
    }

    public ReadOnlySpan<string> Tokens
    {
        get
        {
            return _tokens;
        }
    }
}
