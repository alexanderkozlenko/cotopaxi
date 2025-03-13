// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppFormatHandler : HostCommandHandler
{
    private readonly PackagingService _service;

    public AppFormatHandler(PackagingService service)
    {
        Debug.Assert(service is not null);

        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var sourcePathPattern = commandResult.GetValueForArgument(AppFormatCommand.SourceArgument);
        var sourcePaths = GetFiles(Environment.CurrentDirectory, sourcePathPattern);

        return _service.FormatSourcesAsync(sourcePaths, cancellationToken);
    }
}
