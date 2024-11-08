// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackagePackingHandler : HostCommandHandler
{
    private readonly PackageService _service;

    public PackagePackingHandler(PackageService service)
    {
        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        var projectFile = commandResult.GetValueForArgument(PackagePackingCommand.ProjectArgument);
        var packageFile = commandResult.GetValueForArgument(PackagePackingCommand.PackageArgument);

        return _service.CreatePackageAsync(projectFile, packageFile, cancellationToken);
    }
}
