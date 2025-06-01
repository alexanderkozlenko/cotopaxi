// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;

namespace Cotopaxi.Cosmos.Packaging;

public sealed class DatabasePackageProperties
{
    private readonly PackageProperties _packageProperties;

    internal DatabasePackageProperties(PackageProperties packageProperties)
    {
        Debug.Assert(packageProperties is not null);

        _packageProperties = packageProperties;
    }

    public string? Identifier
    {
        get
        {
            return _packageProperties.Identifier;
        }
        set
        {
            _packageProperties.Identifier = value;
        }
    }

    public DateTime? Created
    {
        get
        {
            return _packageProperties.Created;
        }
        set
        {
            _packageProperties.Created = value;
        }
    }

    public string? Creator
    {
        get
        {
            return _packageProperties.Creator;
        }
        set
        {
            _packageProperties.Creator = value;
        }
    }

    public string? Subject
    {
        get
        {
            return _packageProperties.Subject;
        }
        set
        {
            _packageProperties.Subject = value;
        }
    }

    public string? Version
    {
        get
        {
            return _packageProperties.Version;
        }
        set
        {
            _packageProperties.Version = value;
        }
    }
}
