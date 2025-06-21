// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class DeployCommandLineAction : CommandLineAction<DeployCommand>
{
    private readonly PackageManager _manager;

    public DeployCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(DeployCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var packagePathPattern = result.GetRequiredValue(command.PackageArgument);
        var profilePathPattern = result.GetValue(command.ProfileOption);
        var dryRun = result.GetValue(command.DryRunOption);
        var cosmosAccountEndpoint = result.GetValue(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValue(command.KeyOption);
        var cosmosConnectionString = result.GetValue(command.ConnectionStringOption);

        var packagePaths = PathGlobbing.GetFilePaths(packagePathPattern, Environment.CurrentDirectory);
        var profilePaths = !string.IsNullOrEmpty(profilePathPattern) ? PathGlobbing.GetFilePaths(profilePathPattern, Environment.CurrentDirectory) : null;
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.DeployPackagesAsync(packagePaths, cosmosAuthInfo, profilePaths, dryRun, cancellationToken);
    }
}
