﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly struct JsonPointer
{
    private readonly string[] _tokens;

    public JsonPointer(string value)
    {
        var tokens = value[1..].Split('/');

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
