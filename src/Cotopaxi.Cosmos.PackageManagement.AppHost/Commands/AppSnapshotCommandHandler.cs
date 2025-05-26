// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppSnapshotCommandHandler : CommandHandler<AppSnapshotCommand>
{
    private readonly PackageManager _manager;

    public AppSnapshotCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppSnapshotCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var profilePathPattern = result.GetValueForArgument(command.ProfileArgument);
        var packagePath = Path.GetFullPath(result.GetValueForArgument(command.PackageArgument), Environment.CurrentDirectory);
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);

        var profilePaths = PathGlobbing.GetFilePaths(profilePathPattern, Environment.CurrentDirectory);
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.CreateSnapshotPackageAsync(profilePaths, packagePath, cosmosAuthInfo, cancellationToken);
    }
}
