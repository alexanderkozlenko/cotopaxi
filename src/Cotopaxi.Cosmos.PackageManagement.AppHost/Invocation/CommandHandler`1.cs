// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

internal abstract class CommandHandler<T> : ICommandHandler
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

        try
        {
            return await InvokeAsync((T)result.Command, result, cancellationToken) ? 0 : 1;
        }
        catch (Exception ex)
        {
            var exceptions = new Stack<Exception>();

            UnrollException(ex, exceptions);

            if (!Console.IsOutputRedirected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            try
            {
                while (exceptions.TryPop(out var exception))
                {
                    Console.Error.WriteLine($"Error 0x{exception.HResult:X8}: {exception.Message}");
                }
            }
            finally
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.ResetColor();
                }
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
