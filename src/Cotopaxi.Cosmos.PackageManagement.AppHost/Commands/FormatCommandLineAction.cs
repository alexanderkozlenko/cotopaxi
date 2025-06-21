// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;
using Cotopaxi.Cosmos.PackageManagement.Primitives;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class FormatCommandLineAction : CommandLineAction<FormatCommand>
{
    private readonly PackageManager _manager;

    public FormatCommandLineAction(PackageManager manager)
    {
        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(FormatCommand command, SymbolResult result, CancellationToken cancellationToken)
    {
        var sourcePathPattern = result.GetRequiredValue(command.SourceArgument);
        var sourcePaths = PathGlobbing.GetFilePaths(sourcePathPattern, Environment.CurrentDirectory);

        return _manager.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
