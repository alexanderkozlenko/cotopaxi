// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;

namespace Cotopaxi.Cosmos.Packaging;

public sealed class DatabasePackagePartition
{
    private readonly PackagePart _packagePart;
    private readonly string _databaseName;
    private readonly string _containerName;
    private readonly DatabaseOperationType _operationType;

    internal DatabasePackagePartition(PackagePart packagePart, string databaseName, string containerName, DatabaseOperationType operationType)
    {
        Debug.Assert(packagePart is not null);
        Debug.Assert(databaseName is not null);
        Debug.Assert(containerName is not null);

        _packagePart = packagePart;
        _databaseName = databaseName;
        _containerName = containerName;
        _operationType = operationType;
    }

    public Stream GetStream(FileMode mode, FileAccess access)
    {
        return _packagePart.GetStream(mode, access);
    }

    public Uri Uri
    {
        get
        {
            return _packagePart.Uri;
        }
    }

    public string DatabaseName
    {
        get
        {
            return _databaseName;
        }
    }

    public string ContainerName
    {
        get
        {
            return _containerName;
        }
    }

    public DatabaseOperationType OperationType
    {
        get
        {
            return _operationType;
        }
    }
}
