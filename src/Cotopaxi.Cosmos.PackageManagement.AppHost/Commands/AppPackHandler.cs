// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackHandler : HostCommandHandler<AppPackCommand>
{
    private readonly PackageManager _manager;

    public AppPackHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppPackCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result is not null);

        var projectPath = result.GetValueForArgument(command.ProjectArgument);
        var packagePath = result.GetValueForArgument(command.PackageArgument);
        var packageVersion = result.GetValueForOption(command.VersionOption);

        return _manager.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
