// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

internal abstract class CommandLineAction<T> : AsynchronousCommandLineAction
    where T : Command
{
    public sealed override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var command = (T)parseResult.CommandResult.Command;
        var result = parseResult.CommandResult;

        try
        {
            return await InvokeAsync(command, result, cancellationToken) ? 0x00000000 : 0x00000001;
        }
        catch (Exception exception)
        {
            var configuration = parseResult.InvocationConfiguration;
            var exceptions = new Stack<Exception>();

            UnrollException(exception, exceptions);

            if (!Console.IsOutputRedirected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            try
            {
                while (exceptions.TryPop(out var current))
                {
                    configuration.Error.WriteLine($"Error 0x{current.HResult:X8}: {current.Message}");
                }
            }
            finally
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.ResetColor();
                }
            }

            return 0x00000001;
        }
    }

    protected abstract Task<bool> InvokeAsync(T command, SymbolResult result, CancellationToken cancellationToken);

    private static void UnrollException(Exception exception, Stack<Exception> exceptions)
    {
        var current = exception;

        while (current is not null)
        {
            if (current is not AggregateException aggregate)
            {
                exceptions.Push(current);
                current = current.InnerException;
            }
            else
            {
                exceptions.EnsureCapacity(exceptions.Count + aggregate.InnerExceptions.Count);
                current = null;

                for (var i = 0; i < aggregate.InnerExceptions.Count; i++)
                {
                    UnrollException(aggregate.InnerExceptions[^(i + 1)], exceptions);
                }
            }
        }
    }
}
