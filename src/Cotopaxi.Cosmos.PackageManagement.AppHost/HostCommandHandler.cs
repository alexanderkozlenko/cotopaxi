// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal abstract class HostCommandHandler : ICommandHandler
{
    public int Invoke(InvocationContext context)
    {
        return InvokeAsync(context).GetAwaiter().GetResult();
    }

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        var logger = context.GetHost().Services.GetRequiredService<ILogger<HostCommandHandler>>();
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        logger.LogInformation("Cotopaxi {Version}", version);
        logger.LogInformation("");

        try
        {
            var result = await InvokeAsync(context.ParseResult.CommandResult, context.GetCancellationToken());

            return result ? 0 : 1;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "ERROR: {Message}", ex.Message);

            return 1;
        }
    }

    protected abstract Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken);
}
