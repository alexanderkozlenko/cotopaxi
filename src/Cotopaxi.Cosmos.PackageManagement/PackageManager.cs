// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    private static readonly string s_applicationName = $"cotopaxi/{typeof(PackageManager).Assembly.GetName().Version?.ToString(3)}";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger _logger;

    public PackageManager(ILogger<PackageManager> logger)
    {
        Debug.Assert(logger is not null);

        _logger = logger;
    }

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
