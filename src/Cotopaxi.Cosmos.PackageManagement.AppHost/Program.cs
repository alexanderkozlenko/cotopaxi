// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(ConfigureLogging);
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<PackageManager>();

        services.AddSingleton<CheckpointCommandLineAction>();
        services.AddSingleton<DeployCommandLineAction>();
        services.AddSingleton<DiffCommandLineAction>();
        services.AddSingleton<FormatCommandLineAction>();
        services.AddSingleton<PackCommandLineAction>();
        services.AddSingleton<ShowCommandLineAction>();
        services.AddSingleton<SnapshotCommandLineAction>();

        await using var serviceProvider = services.BuildServiceProvider();

        var command = new RootCommand("The package manager for Azure Cosmos DB")
        {
            CreateCommand<PackCommand, PackCommandLineAction>(),
            CreateCommand<DeployCommand, DeployCommandLineAction>(),
            CreateCommand<CheckpointCommand, CheckpointCommandLineAction>(),
            CreateCommand<SnapshotCommand, SnapshotCommandLineAction>(),
            CreateCommand<DiffCommand, DiffCommandLineAction>(),
            CreateCommand<ShowCommand, ShowCommandLineAction>(),
            CreateCommand<FormatCommand, FormatCommandLineAction>(),
        };

        var commandLine = new CommandLineConfiguration(command)
        {
            ProcessTerminationTimeout = TimeSpan.Zero,
        };

        return await commandLine.InvokeAsync(args).ConfigureAwait(false);

        TCommand CreateCommand<TCommand, TCommandLineAction>()
            where TCommand : Command, new()
            where TCommandLineAction : CommandLineAction<TCommand>
        {
            return new()
            {
                Action = serviceProvider.GetRequiredService<TCommandLineAction>(),
            };
        }

        static void ConfigureLogging(ILoggingBuilder builder)
        {
            builder.AddConsoleFormatter<CommandLineFormatter, ConsoleFormatterOptions>();
            builder.AddConsole(static x => x.FormatterName = nameof(CommandLineFormatter));
            builder.AddFilter("Microsoft", LogLevel.Error);
        }
    }
}
