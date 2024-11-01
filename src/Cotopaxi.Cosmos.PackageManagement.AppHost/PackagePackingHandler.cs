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
        var projectPath = commandResult.GetValueForArgument(PackagePackingCommand.ProjectArgument);
        var packagePath = commandResult.GetValueForArgument(PackagePackingCommand.PackageArgument);

        projectPath = Path.GetFullPath(projectPath, Environment.CurrentDirectory);
        packagePath = Path.GetFullPath(packagePath, Environment.CurrentDirectory);

        return _service.CreatePackageAsync(projectPath, packagePath, cancellationToken);
    }
}
