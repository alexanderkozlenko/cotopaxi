// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppShowCommandHandler : CommandHandler<AppShowCommand>
{
    private readonly PackageManager _manager;

    public AppShowCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppShowCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var packagePathPattern = result.GetValueForArgument(command.PackageArgument);
        var packagePaths = PathGlobbing.GetFilePaths(packagePathPattern, Environment.CurrentDirectory);

        return _manager.ShowPackageInfoAsync(packagePaths, cancellationToken);
    }
}
