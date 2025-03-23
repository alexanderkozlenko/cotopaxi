// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointHandler : HostCommandHandler
{
    private readonly PackageManager _manager;

    public AppCheckpointHandler(PackageManager manager)
    {
        Debug.Assert(manager is not null);

        _manager = manager;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        Debug.Assert(commandResult is not null);

        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppCheckpointCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppCheckpointCommand.KeyOption);
        var cosmosAuthKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
        var cosmosConnectionString = commandResult.GetValueForOption(AppCheckpointCommand.ConnectionStringOption);
        var cosmosConnectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");

        Uri.TryCreate(Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT"), UriKind.Absolute, out var cosmosAccountEndpointVariable);

        var cosmosAuthInfo = default(CosmosAuthInfo);

        if (cosmosAccountEndpoint is not null)
        {
            if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
            {
                cosmosAuthInfo = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
            {
                cosmosAuthInfo = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionString))
        {
            cosmosAuthInfo = new(cosmosConnectionString);
        }
        else if (cosmosAccountEndpointVariable is not null)
        {
            if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
            {
                cosmosAuthInfo = new(cosmosAccountEndpointVariable, cosmosAuthKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
            {
                cosmosAuthInfo = new(cosmosAccountEndpointVariable, cosmosAuthKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionStringVariable))
        {
            cosmosAuthInfo = new(cosmosConnectionStringVariable);
        }

        if (cosmosAuthInfo is null)
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var sourcePackagePathPattern = commandResult.GetValueForArgument(AppCheckpointCommand.PackageArgument);
        var sourcePackagePaths = GetFiles(Environment.CurrentDirectory, sourcePackagePathPattern);
        var rollbackPackagePath = commandResult.GetValueForArgument(AppCheckpointCommand.RollbackPackageArgument);

        return _manager.CreateCheckpointPackagesAsync(sourcePackagePaths, rollbackPackagePath, cosmosAuthInfo, cancellationToken);
    }
}
