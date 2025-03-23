// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppFormatHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var sourcePathPattern = commandResult.GetValueForArgument(AppFormatCommand.SourceArgument);
        var sourcePaths = GetFiles(Environment.CurrentDirectory, sourcePathPattern);

        return _manager.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
