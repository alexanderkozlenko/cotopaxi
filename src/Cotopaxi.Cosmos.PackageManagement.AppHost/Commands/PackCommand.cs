// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class PackCommand : Command
{
    public readonly Argument<string> ProjectArgument = new("project")
    {
        Description = "The path to the project that specifies deployment entries to include",
    };

    public readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The path to a resulting database package",
    };

    public readonly Option<string> VersionOption = new("--version")
    {
        Description = "Sets the package version information",
    };

    public PackCommand()
        : base("pack", "Packs the deployment entries into a database package")
    {
        Arguments.Add(ProjectArgument.AsInputFile());
        Arguments.Add(PackageArgument.AsOutputFile());
        Options.Add(VersionOption);
    }
}
