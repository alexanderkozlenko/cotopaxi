// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

internal sealed class CommandLineFormatter : ConsoleFormatter
{
    public CommandLineFormatter()
        : base(nameof(CommandLineFormatter))
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
    }
}
