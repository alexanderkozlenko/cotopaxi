// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class ShowCommand : Command
{
    public readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The path to the database package to show information about",
    };

    public ShowCommand()
        : base("show", "Shows information about a database package")
    {
        Arguments.Add(PackageArgument.AsInputFile());
    }
}
