// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDeployHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppDeployHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppDeployCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppDeployCommand.KeyOption);
        var cosmosConnectionString = commandResult.GetValueForOption(AppDeployCommand.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var packagePathPattern = commandResult.GetValueForArgument(AppDeployCommand.PackageArgument);
        var packagePaths = GetFiles(Environment.CurrentDirectory, packagePathPattern);
        var dryRun = commandResult.GetValueForOption(AppDeployCommand.DryRunOption);

        return _manager.DeployPackagesAsync(packagePaths, cosmosAuthInfo, dryRun, cancellationToken);
    }
}
