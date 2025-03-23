// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppPackHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var projectPath = commandResult.GetValueForArgument(AppPackCommand.ProjectArgument);
        var packagePath = commandResult.GetValueForArgument(AppPackCommand.PackageArgument);
        var packageVersion = commandResult.GetValueForOption(AppPackCommand.VersionOption);

        return _manager.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
