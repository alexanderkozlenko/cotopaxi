// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatCommand : Command
{
    public readonly Argument<string> SourceArgument = new("source", "The path to a file or files with deployment entries to format and clean up");

    public AppFormatCommand()
        : base("format", "Formats and cleans up the files with deployment entries")
    {
        Add(SourceArgument);
    }
}
