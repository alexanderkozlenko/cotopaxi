// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class ShowCommandLineAction : CommandLineAction<ShowCommand>
{
    private readonly PackageManager _manager;

    public ShowCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(ShowCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var packagePath = Path.GetFullPath(result.GetRequiredValue(command.PackageArgument), Environment.CurrentDirectory);

        return _manager.ShowPackageInfoAsync(packagePath, cancellationToken);
    }
}
