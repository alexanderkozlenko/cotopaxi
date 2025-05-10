// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

internal sealed class LoggingFormatter : ConsoleFormatter
{
    public LoggingFormatter()
        : base(nameof(LoggingFormatter))
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
    }
}
