// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement;

public readonly struct CosmosResourceName
{
    private readonly string? _value;

    public CosmosResourceName(string value)
    {
        if (!IsWellFormed(value))
        {
            throw new ArgumentException("The value is not a well-formed resource name", nameof(value));
        }

        _value = value;
    }

    private static bool IsWellFormed([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Length > 255)
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
