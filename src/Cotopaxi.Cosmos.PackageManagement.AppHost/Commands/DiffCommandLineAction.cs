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
        var package2Value = result.GetValue(command.Package2Argument);
        var diffFilter = result.GetValue(command.DiffFilterOption);
        var profilePath = result.GetValue(command.ProfileOption);
        var useExitCode = result.GetValue(command.ExitCodeOption);
        var cosmosAccountEndpoint = result.GetValue(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValue(command.KeyOption);
        var cosmosConnectionString = result.GetValue(command.ConnectionStringOption);

        profilePath = !string.IsNullOrEmpty(profilePath) ? Path.GetFullPath(profilePath, Environment.CurrentDirectory) : null;

        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        if (!string.IsNullOrEmpty(package2Value))
        {
            var package2Path = Path.GetFullPath(package2Value, Environment.CurrentDirectory);

            return _manager.ComparePackagesAsync(package1Path, package2Path, cosmosAuthInfo, diffFilter, profilePath, useExitCode, cancellationToken);
        }
        else
        {
            return _manager.ComparePackageWithDatabaseAsync(package1Path, cosmosAuthInfo, diffFilter, profilePath, useExitCode, cancellationToken);
        }
    }
}
