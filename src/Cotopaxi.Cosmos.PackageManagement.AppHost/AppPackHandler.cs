// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppPackHandler : HostCommandHandler
{
    private readonly PackagingService _service;

    public AppPackHandler(PackagingService service)
    {
        Debug.Assert(service is not null);

        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var projectPath = commandResult.GetValueForArgument(AppPackCommand.ProjectArgument);
        var packagePath = commandResult.GetValueForArgument(AppPackCommand.PackageArgument);
        var packageVersion = commandResult.GetValueForOption(AppPackCommand.VersionOption);

        return _service.CreatePackageAsync(projectPath, packagePath, packageVersion, cancellationToken);
    }
}
