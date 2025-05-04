// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDiffHandler : HostCommandHandler<AppDiffCommand>
{
    private readonly PackageManager _manager;

    public AppDiffHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppDiffCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = result.GetValueForOption(command.EndpointOption);
        var cosmosAuthKeyOrResourceToken = result.GetValueForOption(command.KeyOption);
        var cosmosConnectionString = result.GetValueForOption(command.ConnectionStringOption);
        var package1Path = Path.GetFullPath(result.GetValueForArgument(command.Package1Argument), Environment.CurrentDirectory);
        var package2Path = Path.GetFullPath(result.GetValueForArgument(command.Package2Argument), Environment.CurrentDirectory);
        var profilePath = result.GetValueForOption(command.ProfileOption);
        var useExitCode = result.GetValueForOption(command.ExitCodeOption);

        profilePath = !string.IsNullOrEmpty(profilePath) ? Path.GetFullPath(profilePath, Environment.CurrentDirectory) : null;

        var cosmosAuthInfo = CosmosAuthInfoFactory.CreateAuthInfo(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken, cosmosConnectionString);

        return _manager.ComparePackagesAsync(package1Path, package2Path, cosmosAuthInfo, profilePath, useExitCode, cancellationToken);
    }
}
