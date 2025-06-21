// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class FormatCommand : Command
{
    public readonly Argument<string> SourceArgument = new("source")
    {
        Description = "The path to a source or sources with deployment entries to format and prune",
    };

    public FormatCommand()
        : base("format", "Formats and prunes the sources with deployment entries")
    {
        Arguments.Add(SourceArgument);
    }
}
