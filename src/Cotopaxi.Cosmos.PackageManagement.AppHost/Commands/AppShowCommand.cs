// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppShowCommand : Command
{
    public readonly Argument<string> PackageArgument =
        new("package", "The path to the database package to show information about");

    public AppShowCommand()
        : base("show", "Shows information about a database package")
    {
        PackageArgument.AddValidationAsInputFile();

        AddArgument(PackageArgument);
    }
}
