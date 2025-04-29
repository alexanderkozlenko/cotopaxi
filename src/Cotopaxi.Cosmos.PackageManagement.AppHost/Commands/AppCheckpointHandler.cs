// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointHandler : HostCommandHandler<AppCheckpointCommand>
{
    private readonly PackageManager _manager;

    public AppCheckpointHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppCheckpointCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result is not null);

        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var sourcePackagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var sourcePackagePaths = PackageManager.GetFiles(Environment.CurrentDirectory, sourcePackagePathPattern);
        var rollbackPackagePath = result.GetValueForArgument(command.RollbackPackageArgument);

        return _manager.CreateCheckpointPackagesAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
