// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackagePackingCommand : Command
{
    public static readonly Argument<FileInfo> ProjectArgument = new("project", "The project to create a package from");
    public static readonly Argument<FileInfo> PackageArgument = new("package", "The package to create");

    public PackagePackingCommand()
        : base("pack", "Creates a package for an Azure Cosmos DB account")
    {
    }
}
