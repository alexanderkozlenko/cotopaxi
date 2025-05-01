// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppShowHandler : HostCommandHandler<AppShowCommand>
{
    private readonly PackageManager _manager;

    public AppShowHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppShowCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var packagePath = Path.GetFullPath(result.GetValueForArgument(command.PackageArgument), Environment.CurrentDirectory);

        return _manager.ShowPackageInfoAsync(packagePath, cancellationToken);
    }
}
