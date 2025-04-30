// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackHandler : HostCommandHandler<AppPackCommand>
{
    private readonly PackageManager _manager;

    public AppPackHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppPackCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var projectPath = result.GetValueForArgument(command.ProjectArgument);
        var packagePath = result.GetValueForArgument(command.PackageArgument);
        var packageVersion = result.GetValueForOption(command.VersionOption);

        return _manager.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
