// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

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
        var packagePath = Path.GetFullPath(result.GetValueForArgument(command.PackageArgument), Environment.CurrentDirectory);

        return _manager.ShowPackageInfoAsync(packagePath, cancellationToken);
    }
}
