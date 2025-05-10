// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointCommandHandler : CommandHandler<AppCheckpointCommand>
{
    private readonly PackageManager _manager;

    public AppCheckpointCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppCheckpointCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);
        var sourcePackagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var rollbackPackagePath = Path.GetFullPath(result.GetValueForArgument(command.RollbackPackageArgument), Environment.CurrentDirectory);

        var sourcePackagePaths = PathGlobbing.GetFilePaths(sourcePackagePathPattern, Environment.CurrentDirectory);
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.CreateCheckpointPackagesAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
