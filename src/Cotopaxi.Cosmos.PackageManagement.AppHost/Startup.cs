// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Hosting;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal static class Startup
{
    public static RootCommand CreateCommand()
    {
        return new("The package manager for Azure Cosmos DB")
        {
            new AppPackCommand(),
            new AppDeployCommand(),
            new AppCheckpointCommand(),
            new AppSnapshotCommand(),
            new AppDiffCommand(),
            new AppShowCommand(),
            new AppFormatCommand(),
        };
    }

    public static void ConfigureHost(IHostBuilder builder)
    {
        builder
            .ConfigureServices(ConfigureServices);

        builder
            .UseCommandHandler<AppPackCommand, AppPackCommandHandler>()
            .UseCommandHandler<AppDeployCommand, AppDeployCommandHandler>()
            .UseCommandHandler<AppCheckpointCommand, AppCheckpointCommandHandler>()
            .UseCommandHandler<AppSnapshotCommand, AppSnapshotCommandHandler>()
            .UseCommandHandler<AppDiffCommand, AppDiffCommandHandler>()
            .UseCommandHandler<AppShowCommand, AppShowCommandHandler>()
            .UseCommandHandler<AppFormatCommand, AppFormatCommandHandler>();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSingleton<PackageManager>();
    }
}
