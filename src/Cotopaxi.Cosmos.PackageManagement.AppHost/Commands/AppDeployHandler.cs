// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDeployHandler : HostCommandHandler<AppDeployCommand>
{
    private readonly PackageManager _manager;

    public AppDeployHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppDeployCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);
        var packagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var profilePathPattern = result.GetValueForOption(command.ProfileOption);
        var dryRun = result.GetValueForOption(command.DryRunOption);

        var packagePaths = PathGlobbing.GetFilePaths(packagePathPattern, Environment.CurrentDirectory);
        var profilePaths = !string.IsNullOrEmpty(profilePathPattern) ? PathGlobbing.GetFilePaths(profilePathPattern, Environment.CurrentDirectory) : null;
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.DeployPackagesAsync(packagePaths, cosmosAuthInfo, profilePaths, dryRun, cancellationToken);
    }
}
