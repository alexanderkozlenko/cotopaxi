// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    private static readonly string s_applicationName = $"cotopaxi/{typeof(PackageManager).Assembly.GetName().Version?.ToString(3)}";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public PackageManager(TimeProvider timeProvider, ILogger<PackageManager> logger)
    {
        Debug.Assert(timeProvider is not null);
        Debug.Assert(logger is not null);

        _timeProvider = timeProvider;
        _logger = logger;
    }
}
