// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return new CommandLineBuilder(Startup.CreateCommand())
            .UseHost(static _ => Host.CreateDefaultBuilder(), ConfigureHost)
            .CancelOnProcessTermination()
            .UseParseErrorReporting()
            .UseExceptionHandler(HandleException)
            .UseVersionOption()
            .UseHelp(["-h", "--help"])
            .Build()
            .InvokeAsync(args);

        static void ConfigureHost(IHostBuilder builder)
        {
            builder
                .ConfigureServices(static x => x.AddSingleton(TimeProvider.System))
                .ConfigureLogging(ConfigureLogging);

            Startup.ConfigureHost(builder);
        }

        static void ConfigureLogging(ILoggingBuilder builder)
        {
            builder
                .AddConsoleFormatter<LoggingFormatter, ConsoleFormatterOptions>()
                .AddConsole(static x => x.FormatterName = nameof(LoggingFormatter))
                .AddFilter("Microsoft", LogLevel.Error);
        }
    }

    private static void HandleException(Exception exception, InvocationContext context)
    {
        context.ExitCode = 0x00000001;

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
                Console.Error.WriteLine($"Error 0x{current.HResult:X8}: {current.Message}");
            }
        }
        finally
        {
            if (!Console.IsOutputRedirected)
            {
                Console.ResetColor();
            }
        }
    }

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
