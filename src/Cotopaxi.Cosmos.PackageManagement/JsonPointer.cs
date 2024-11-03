// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement;

public readonly struct JsonPointer
{
    private readonly string[]? _tokens;

    public JsonPointer(string? value)
    {
        if (value is { Length: > 0 })
        {
            var tokens = value.TrimStart('/').Split('/');

            for (var i = 0; i < tokens.Length; i++)
            {
                tokens[i] = tokens[i]
                    .Replace("~1", "/", StringComparison.Ordinal)
                    .Replace("~0", "~", StringComparison.Ordinal);
            }

            _tokens = tokens;
        }
    }

    public ReadOnlySpan<string> Tokens
    {
        get
        {
            return _tokens;
        }
    }
}
