// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppFormatCommand : Command
{
    public readonly Argument<string> SourceArgument = new("source", "The path to a source or sources with deployment entries to format and prune");

    public AppFormatCommand()
        : base("format", "Formats and prunes the sources with deployment entries")
    {
        Add(SourceArgument);
    }
}
