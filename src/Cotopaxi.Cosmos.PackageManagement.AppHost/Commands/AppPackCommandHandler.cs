// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackCommandHandler : CommandHandler<AppPackCommand>
{
    private readonly PackageManager _manager;

    public AppPackCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppPackCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(result.GetValueForArgument(command.ProjectArgument), Environment.CurrentDirectory);
        var packagePath = Path.GetFullPath(result.GetValueForArgument(command.PackageArgument), Environment.CurrentDirectory);
        var packageVersion = result.GetValueForOption(command.VersionOption);

        return _manager.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
