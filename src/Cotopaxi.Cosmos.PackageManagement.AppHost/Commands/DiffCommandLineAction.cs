// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class DiffCommandLineAction : CommandLineAction<DiffCommand>
{
    private readonly PackageManager _manager;

    public DiffCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(DiffCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var package1Path = Path.GetFullPath(result.GetRequiredValue(command.Package1Argument), Environment.CurrentDirectory);
        var package2Path = Path.GetFullPath(result.GetRequiredValue(command.Package2Argument), Environment.CurrentDirectory);
        var profilePath = result.GetValue(command.ProfileOption);
        var useExitCode = result.GetValue(command.ExitCodeOption);
        var cosmosAccountEndpoint = result.GetValue(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValue(command.KeyOption);
        var cosmosConnectionString = result.GetValue(command.ConnectionStringOption);

        profilePath = !string.IsNullOrEmpty(profilePath) ? Path.GetFullPath(profilePath, Environment.CurrentDirectory) : null;

        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.ComparePackagesAsync(package1Path, package2Path, cosmosAuthInfo, profilePath, useExitCode, cancellationToken);
    }
}
