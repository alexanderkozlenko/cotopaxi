// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointHandler : HostCommandHandler<AppCheckpointCommand>
{
    private readonly PackageManager _manager;

    public AppCheckpointHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppCheckpointCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);
        var sourcePackagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var sourcePackagePaths = PackageManager.GetFiles(Environment.CurrentDirectory, sourcePackagePathPattern);
        var rollbackPackagePath = result.GetValueForArgument(command.RollbackPackageArgument);

        return _manager.CreateCheckpointPackagesAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
