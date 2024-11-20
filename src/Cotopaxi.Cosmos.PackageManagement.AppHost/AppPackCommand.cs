// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppPackCommand : Command
{
    public static readonly Argument<string> ProjectArgument = new("project", "The path to the project that specifies documents to pack");
    public static readonly Argument<string> PackageArgument = new("package", "The path to the package to create");
    public static readonly Option<string> VersionOption = new("--version", "Sets the package version information");

    public AppPackCommand()
        : base("pack", "Packs the documents into an Azure Cosmos DB package")
    {
    }
}
