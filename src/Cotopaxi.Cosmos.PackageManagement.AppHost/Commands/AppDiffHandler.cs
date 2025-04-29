// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDiffHandler : HostCommandHandler<AppDiffCommand>
{
    private readonly PackageManager _manager;

    public AppDiffHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppDiffCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result is not null);

        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);

        if (!CosmosAuthInfoFactory.TryGetCreateCosmosAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString, out var cosmosAuthInfo))
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var package1Path = Path.GetFullPath(result.GetValueForArgument(command.Package1Argument), Environment.CurrentDirectory);
        var package2Path = Path.GetFullPath(result.GetValueForArgument(command.Package2Argument), Environment.CurrentDirectory);
        var profilePath = result.GetValueForOption(command.ProfileOption);
        var useExitCode = result.GetValueForOption(command.ExitCodeOption);

        profilePath = !string.IsNullOrEmpty(profilePath) ? Path.GetFullPath(profilePath, Environment.CurrentDirectory) : null;

        return _manager.ComparePackagesAsync(package1Path, package2Path, cosmosAuthInfo, profilePath, useExitCode, cancellationToken);
    }
}
