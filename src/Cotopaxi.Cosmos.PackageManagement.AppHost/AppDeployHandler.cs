// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppDeployHandler : HostCommandHandler
{
    private readonly PackagingService _service;

    public AppDeployHandler(PackagingService service)
    {
        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppDeployCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppDeployCommand.KeyOption);
        var cosmosAuthKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
        var cosmosConnectionString = commandResult.GetValueForOption(AppDeployCommand.ConnectionStringOption);
        var cosmosConnectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");
        var dryRun = commandResult.GetValueForOption(AppDeployCommand.DryRunOption);

        Uri.TryCreate(Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT"), UriKind.Absolute, out var cosmosAccountEndpointVariable);

        var cosmosCredential = default(CosmosCredential);

        if (cosmosAccountEndpoint is not null)
        {
            if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
            {
                cosmosCredential = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
            {
                cosmosCredential = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionString))
        {
            cosmosCredential = new(cosmosConnectionString);
        }
        else if (cosmosAccountEndpointVariable is not null)
        {
            if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
            {
                cosmosCredential = new(cosmosAccountEndpointVariable, cosmosAuthKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
            {
                cosmosCredential = new(cosmosAccountEndpointVariable, cosmosAuthKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionStringVariable))
        {
            cosmosCredential = new(cosmosConnectionStringVariable);
        }

        if (cosmosCredential is null)
        {
            throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
        }

        var packagePathPattern = commandResult.GetValueForArgument(AppDeployCommand.PackageArgument);
        var packagePaths = GetFiles(Environment.CurrentDirectory, packagePathPattern);

        return _service.DeployPackagesAsync(packagePaths, cosmosCredential, dryRun, cancellationToken);
    }
}
