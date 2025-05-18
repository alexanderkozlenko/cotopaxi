// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Invocation;

internal abstract class CommandHandler<T> : ICommandHandler
    where T : Command
{
    public int Invoke(InvocationContext context)
    {
        throw new InvalidOperationException();
    }

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        var cancellationToken = context.GetCancellationToken();
        var command = (T)context.ParseResult.CommandResult.Command;
        var result = context.ParseResult.CommandResult;

        return await InvokeAsync(command, result, cancellationToken) ? 0x00000000 : 0x00000001;
    }

    protected abstract Task<bool> InvokeAsync(T command, SymbolResult result, CancellationToken cancellationToken);
}
