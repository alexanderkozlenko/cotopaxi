// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class PackCommandLineAction : CommandLineAction<PackCommand>
{
    private readonly PackageManager _manager;

    public PackCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(PackCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(result.GetRequiredValue(command.ProjectArgument), Environment.CurrentDirectory);
        var packagePath = Path.GetFullPath(result.GetRequiredValue(command.PackageArgument), Environment.CurrentDirectory);
        var packageVersion = result.GetValue(command.VersionOption);

        return _manager.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
