// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackCommand : Command
{
    public readonly Argument<string> ProjectArgument =
        new("project", "The path to the project that specifies deployment entries to include");
    public readonly Argument<string> PackageArgument =
        new("package", "The path to a resulting database package");
    public readonly Option<string> VersionOption =
        new("--version", "Sets the package version information");

    public AppPackCommand()
        : base("pack", "Packs the deployment entries into a database package")
    {
        ProjectArgument.AddValidationAsInputFile();
        PackageArgument.AddValidationAsOutputFile();

        AddArgument(ProjectArgument);
        AddArgument(PackageArgument);
        AddOption(VersionOption);
    }
}
