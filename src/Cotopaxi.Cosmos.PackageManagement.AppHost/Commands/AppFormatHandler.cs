// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatHandler : HostCommandHandler<AppFormatCommand>
{
    private readonly PackageManager _manager;

    public AppFormatHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppFormatCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var sourcePathPattern = result.GetValueForArgument(command.SourceArgument);
        var sourcePaths = GlobbingMatcher.GetFiles(Environment.CurrentDirectory, sourcePathPattern);

        return _manager.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
