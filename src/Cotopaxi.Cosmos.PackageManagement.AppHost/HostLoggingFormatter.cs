// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
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
        Debug.Assert(textWriter is not null);

        textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
    }
}
