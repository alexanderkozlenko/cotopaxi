// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal abstract class HostCommandHandler : ICommandHandler
{
    public int Invoke(InvocationContext context)
    {
        throw new NotSupportedException();
    }

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        var logger = context.GetHost().Services.GetRequiredService<ILogger<HostCommandHandler>>();

        try
        {
            var result = await InvokeAsync(context.ParseResult.CommandResult, context.GetCancellationToken());

            return result ? 0 : 1;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error: {Message}", ex.Message);

            return 1;
        }
    }

    protected abstract Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken);

    protected static string[] GetFiles(string path, string searchPattern)
    {
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
