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
            new AppFormatCommand(),
        };

        var builder = new CommandLineBuilder(command)
            .UseHost(CreateHostBuilder, ConfigureHostBuilder)
            .CancelOnProcessTermination()
            .UseParseErrorReporting()
            .UseExceptionHandler()
            .UseVersionOption()
            .UseHelp(["-h", "--help"]);

        var parser = builder.Build();

        return parser.InvokeAsync(args);
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder();
    }

    private static void ConfigureHostBuilder(IHostBuilder builder)
    {
        builder
            .ConfigureLogging(ConfigureLogging)
            .ConfigureServices(ConfigureServices)
            .UseCommandHandler<AppPackCommand, AppPackHandler>()
            .UseCommandHandler<AppDeployCommand, AppDeployHandler>()
            .UseCommandHandler<AppCheckpointCommand, AppCheckpointHandler>()
            .UseCommandHandler<AppDiffCommand, AppDiffHandler>()
            .UseCommandHandler<AppFormatCommand, AppFormatHandler>();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSingleton<PackageManager>();
    }

    private static void ConfigureLogging(ILoggingBuilder builder)
    {
        builder
            .AddConsoleFormatter<HostLoggingFormatter, ConsoleFormatterOptions>()
            .AddConsole(ConfigureLoggerOptions)
            .AddFilter("Microsoft", LogLevel.Error);
    }

    private static void ConfigureLoggerOptions(ConsoleLoggerOptions options)
    {
        options.FormatterName = nameof(HostLoggingFormatter);
    }
}
