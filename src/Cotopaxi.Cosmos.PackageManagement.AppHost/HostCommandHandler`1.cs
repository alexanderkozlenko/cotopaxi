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

        var cancellationToken = context.GetCancellationToken();
        var result = context.ParseResult.CommandResult;
        var logger = context.GetHost().Services.GetRequiredService<ILogger<HostCommandHandler<T>>>();

        try
        {
            return await InvokeAsync((T)result.Command, result, cancellationToken) ? 0 : 1;
        }
        catch (Exception ex)
        {
            var exceptions = new Stack<Exception>();

            UnrollException(ex, exceptions);

            while (exceptions.TryPop(out var exception))
            {
                logger.LogError(exception, "Error 0x{HRESULT:X8}: {Message}", exception.HResult, exception.Message);
            }

            return 1;
        }
    }

    protected abstract Task<bool> InvokeAsync(T command, SymbolResult result, CancellationToken cancellationToken);

    private static void UnrollException(Exception exception, Stack<Exception> exceptions)
    {
        var current = exception;

        while (current is not null)
        {
            if (current is AggregateException aggregate)
            {
                for (var i = 0; i < aggregate.InnerExceptions.Count; i++)
                {
                    UnrollException(aggregate.InnerExceptions[^(i + 1)], exceptions);
                }

                current = null;
            }
            else
            {
                exceptions.Push(current);
                current = current.InnerException;
            }
        }
    }
}
