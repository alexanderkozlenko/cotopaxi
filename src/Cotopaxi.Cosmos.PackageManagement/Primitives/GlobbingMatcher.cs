// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Cotopaxi.Cosmos.PackageManagement.Primitives;

public static class GlobbingMatcher
{
    public static string[] GetFiles(string path, string searchPattern)
    {
        Debug.Assert(path is not null);
        Debug.Assert(searchPattern is not null);

        if (Path.IsPathRooted(searchPattern))
        {
            path = Path.GetPathRoot(searchPattern)!;
            searchPattern = Path.GetRelativePath(path, searchPattern);
        }

        var matcher = new Matcher().AddInclude(searchPattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(new(path)));

        return match.Files
            .Select(x => Path.GetFullPath(Path.Combine(path, x.Path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
