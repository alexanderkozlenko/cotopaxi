// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppPackCommand : Command
{
    public static readonly Argument<string> ProjectArgument = new("project", "The path to the project that specifies deployment entries to include");
    public static readonly Argument<string> PackageArgument = new("package", "The path to a resulting database package");
    public static readonly Option<string> VersionOption = new("--version", "Sets the package version information");

    public AppPackCommand()
        : base("pack", "Packs the deployment entries into a database package")
    {
    }
}
