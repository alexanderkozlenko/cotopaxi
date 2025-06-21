// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class SnapshotCommandLineAction : CommandLineAction<SnapshotCommand>
{
    private readonly PackageManager _manager;

    public SnapshotCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(SnapshotCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var profilePathPattern = result.GetRequiredValue(command.ProfileArgument);
        var packagePath = Path.GetFullPath(result.GetRequiredValue(command.PackageArgument), Environment.CurrentDirectory);
        var cosmosAccountEndpoint = result.GetValue(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValue(command.KeyOption);
        var cosmosConnectionString = result.GetValue(command.ConnectionStringOption);

        var profilePaths = PathGlobbing.GetFilePaths(profilePathPattern, Environment.CurrentDirectory);
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.CreateSnapshotPackageAsync(profilePaths, packagePath, cosmosAuthInfo, cancellationToken);
    }
}
