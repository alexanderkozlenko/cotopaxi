// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Cotopaxi.Cosmos.PackageManagement.Primitives;

public static class PathGlobbing
{
    public static string[] GetFilePaths(string searchPattern, string basePath)
    {
        Debug.Assert(searchPattern is not null);
        Debug.Assert(basePath is not null);

        if (Path.IsPathRooted(searchPattern))
        {
            basePath = Path.GetPathRoot(searchPattern)!;
            searchPattern = Path.GetRelativePath(basePath, searchPattern);
        }

        var matcher = new Matcher().AddInclude(searchPattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(new(basePath)));

        return match.Files
            .Select(x => Path.GetFullPath(Path.Combine(basePath, x.Path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
