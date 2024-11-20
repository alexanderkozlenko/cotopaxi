// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackagingService
{
    private static readonly string s_applicationName = $"cotopaxi/{typeof(PackagingService).Assembly.GetName().Version?.ToString(3)}";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger _logger;

    public PackagingService(ILogger<PackagingService> logger)
    {
        Debug.Assert(logger is not null);

        _logger = logger;
    }

    private static string[] GetFiles(string path, string searchPattern)
    {
        var matcher = new Matcher().AddInclude(searchPattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(new(path)));

        return match.Files
            .Select(x => Path.GetFullPath(Path.Combine(path, x.Path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Guid CreateUUID(string source)
    {
        var hash = XxHash128.Hash(Encoding.Unicode.GetBytes(source));

        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new(hash);
    }
}
