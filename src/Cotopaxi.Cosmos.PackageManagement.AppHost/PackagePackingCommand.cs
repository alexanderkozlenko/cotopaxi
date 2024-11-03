// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackagePackingCommand : Command
{
    public static readonly Argument<FileInfo> ProjectArgument = new("project", "Specifies the input project path");
    public static readonly Argument<FileInfo> PackageArgument = new("package", "Specifies the output package path");

    public PackagePackingCommand()
        : base("pack", "Creates a data package for Azure Cosmos DB")
    {
    }
}
