// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class CheckpointCommandLineAction : CommandLineAction<CheckpointCommand>
{
    private readonly PackageManager _manager;

    public CheckpointCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CheckpointCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var sourcePackagePathPattern = result.GetRequiredValue(command.PackageArgument);
        var rollbackPackagePath = Path.GetFullPath(result.GetRequiredValue(command.RollbackPackageArgument), Environment.CurrentDirectory);
        var cosmosAccountEndpoint = result.GetValue(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValue(command.KeyOption);
        var cosmosConnectionString = result.GetValue(command.ConnectionStringOption);

        var sourcePackagePaths = PathGlobbing.GetFilePaths(sourcePackagePathPattern, Environment.CurrentDirectory);
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.CreateRollbackPackageAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
