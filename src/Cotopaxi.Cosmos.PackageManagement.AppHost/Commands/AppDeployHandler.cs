// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDeployHandler : HostCommandHandler<AppDeployCommand>
{
    private readonly PackageManager _manager;

    public AppDeployHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppDeployCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result is not null);

        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var packagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var packagePaths = PackageManager.GetFiles(Environment.CurrentDirectory, packagePathPattern);
        var profilePathPattern = result.GetValueForOption(command.ProfileOption);
        var profilePaths = !string.IsNullOrEmpty(profilePathPattern) ? PackageManager.GetFiles(Environment.CurrentDirectory, profilePathPattern) : null;
        var dryRun = result.GetValueForOption(command.DryRunOption);

        return _manager.DeployPackagesAsync(packagePaths, cosmosAuthInfo, profilePaths, dryRun, cancellationToken);
    }
}
