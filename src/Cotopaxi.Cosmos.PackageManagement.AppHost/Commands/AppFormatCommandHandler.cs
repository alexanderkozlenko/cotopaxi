// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatCommandHandler : CommandHandler<AppFormatCommand>
{
    private readonly PackageManager _manager;

    public AppFormatCommandHandler(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(AppFormatCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var sourcePathPattern = result.GetValueForArgument(command.SourceArgument);
        var sourcePaths = PathGlobbing.GetFilePaths(sourcePathPattern, Environment.CurrentDirectory);

        return _manager.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
