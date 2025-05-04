// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var command = new RootCommand("The package manager for Azure Cosmos DB")
        {
            new AppPackCommand(),
            new AppDeployCommand(),
            new AppCheckpointCommand(),
            new AppDiffCommand(),
            new AppShowCommand(),
            new AppFormatCommand(),
        };

        return new CommandLineBuilder(command)
            .UseHost(static _ => Host.CreateDefaultBuilder(), ConfigureHostBuilder)
            .CancelOnProcessTermination()
            .UseParseErrorReporting()
            .UseExceptionHandler()
            .UseVersionOption()
            .UseHelp(["-h", "--help"])
            .Build()
            .InvokeAsync(args);

        static void ConfigureHostBuilder(IHostBuilder builder)
        {
            builder
                .ConfigureLogging(ConfigureLogging)
                .ConfigureServices(ConfigureServices)
                .UseCommandHandler<AppPackCommand, AppPackHandler>()
                .UseCommandHandler<AppDeployCommand, AppDeployHandler>()
                .UseCommandHandler<AppCheckpointCommand, AppCheckpointHandler>()
                .UseCommandHandler<AppDiffCommand, AppDiffHandler>()
                .UseCommandHandler<AppShowCommand, AppShowHandler>()
                .UseCommandHandler<AppFormatCommand, AppFormatHandler>();
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton<PackageManager>();
        }

        static void ConfigureLogging(ILoggingBuilder builder)
        {
            builder
                .AddConsoleFormatter<HostLoggingFormatter, ConsoleFormatterOptions>()
                .AddConsole(static x => x.FormatterName = nameof(HostLoggingFormatter))
                .AddFilter("Microsoft", LogLevel.Error);
        }
    }
}
