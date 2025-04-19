// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDiffHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppDiffHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppDiffCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppDiffCommand.KeyOption);
        var cosmosConnectionString = commandResult.GetValueForOption(AppDiffCommand.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var package1Path = Path.GetFullPath(commandResult.GetValueForArgument(AppDiffCommand.Package1Argument), Environment.CurrentDirectory);
        var package2Path = Path.GetFullPath(commandResult.GetValueForArgument(AppDiffCommand.Package2Argument), Environment.CurrentDirectory);
        var useExitCode = commandResult.GetValueForOption(AppDiffCommand.ExitCodeOption);

        return _manager.ComparePackagesAsync(package1Path, package2Path, cosmosAuthInfo, useExitCode, cancellationToken);
    }
}
