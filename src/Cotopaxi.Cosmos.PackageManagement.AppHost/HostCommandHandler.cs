// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
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
            logger.LogCritical(ex, "An unexpected error: {Message}", ex.Message);

            return 1;
        }
    }

    protected abstract Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken);
}
