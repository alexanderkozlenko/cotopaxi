// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal abstract class HostCommandHandler<T> : ICommandHandler
    where T : Command
{
    public int Invoke(InvocationContext context)
    {
        throw new NotSupportedException();
    }

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        Debug.Assert(context is not null);

        var result = context.ParseResult.CommandResult;
        var cancellationToken = context.GetCancellationToken();
        var logger = context.GetHost().Services.GetRequiredService<ILogger<HostCommandHandler<T>>>();

        try
        {
            return await InvokeAsync((T)result.Command, result, cancellationToken) ? 0 : 1;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error: {Message}", ex.Message);

            return 1;
        }
    }

    protected abstract Task<bool> InvokeAsync(T command, SymbolResult result, CancellationToken cancellationToken);
}
