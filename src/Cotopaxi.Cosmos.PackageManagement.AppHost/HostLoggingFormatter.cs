// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class HostLoggingFormatter : ConsoleFormatter
{
    public HostLoggingFormatter()
        : base(nameof(HostLoggingFormatter))
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
    }
}
