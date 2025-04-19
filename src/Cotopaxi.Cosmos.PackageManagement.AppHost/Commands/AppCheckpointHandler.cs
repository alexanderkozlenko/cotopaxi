// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppCheckpointHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppCheckpointCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppCheckpointCommand.KeyOption);
        var cosmosConnectionString = commandResult.GetValueForOption(AppCheckpointCommand.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var sourcePackagePathPattern = commandResult.GetValueForArgument(AppCheckpointCommand.PackageArgument);
        var sourcePackagePaths = GetFiles(Environment.CurrentDirectory, sourcePackagePathPattern);
        var rollbackPackagePath = commandResult.GetValueForArgument(AppCheckpointCommand.RollbackPackageArgument);

        return _manager.CreateCheckpointPackagesAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
