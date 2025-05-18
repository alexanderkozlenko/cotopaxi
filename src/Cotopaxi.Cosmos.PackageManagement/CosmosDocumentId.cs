// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Cotopaxi.Cosmos.PackageManagement;

public readonly struct CosmosDocumentId
{
    private static readonly char[] s_restrictedChars = ['/', '\\'];

    private readonly string? _value;

    public CosmosDocumentId(string value)
    {
        if (!IsWellFormed(value))
        {
            throw new ArgumentException($"The value '{value}' is not a well-formed document identifier", nameof(value));
        }

        _value = value;
    }

    public static bool IsWellFormed([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.IndexOfAny(s_restrictedChars) >= 0)
        {
            return false;
        }

        if (Encoding.UTF8.GetByteCount(value) > 1023)
        {
            return false;
        }

        return true;
    }

    public string Value
    {
        get
        {
            return _value ?? throw new InvalidOperationException();
        }
    }
}
