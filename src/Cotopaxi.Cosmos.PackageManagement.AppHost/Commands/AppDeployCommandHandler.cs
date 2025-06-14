﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDeployCommandHandler : CommandHandler<AppDeployCommand>
{
    private readonly PackageManager _manager;

    public AppDeployCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppDeployCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var packagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var profilePathPattern = result.GetValueForOption(command.ProfileOption);
        var dryRun = result.GetValueForOption(command.DryRunOption);
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);

        var packagePaths = PathGlobbing.GetFilePaths(packagePathPattern, Environment.CurrentDirectory);
        var profilePaths = !string.IsNullOrEmpty(profilePathPattern) ? PathGlobbing.GetFilePaths(profilePathPattern, Environment.CurrentDirectory) : null;
        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.DeployPackagesAsync(packagePaths, cosmosAuthInfo, profilePaths, dryRun, cancellationToken);
    }
}
