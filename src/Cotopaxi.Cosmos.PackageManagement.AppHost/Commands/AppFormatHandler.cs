// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatHandler : HostCommandHandler<AppFormatCommand>
{
    private readonly PackageManager _manager;

    public AppFormatHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppFormatCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result is not null);

        var sourcePathPattern = result.GetValueForArgument(command.SourceArgument);
        var sourcePaths = PackageManager.GetFiles(Environment.CurrentDirectory, sourcePathPattern);

        return _manager.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
