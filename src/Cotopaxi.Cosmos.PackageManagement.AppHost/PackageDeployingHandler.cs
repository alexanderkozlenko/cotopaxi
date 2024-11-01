// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackageDeployingHandler : HostCommandHandler
{
    private readonly PackageService _service;

    public PackageDeployingHandler(PackageService service)
    {
        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        var packagePath = commandResult.GetValueForArgument(PackageDeployingCommand.PackageArgument);
        var cosmosConnectionString = commandResult.GetValueForOption(PackageDeployingCommand.ConnectionStringOption);
        var cosmosAccountEndpoint = commandResult.GetValueForOption(PackageDeployingCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(PackageDeployingCommand.KeyOption);

        packagePath = Path.GetFullPath(packagePath, Environment.CurrentDirectory);

        if (string.IsNullOrEmpty(cosmosConnectionString))
        {
            cosmosConnectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
        }
        if (string.IsNullOrEmpty(cosmosAccountEndpoint))
        {
            cosmosAccountEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
        }
        if (string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
        {
            cosmosAuthKeyOrResourceToken = Environment.GetEnvironmentVariable("COSMOS_KEY");
        }

        var cosmosCredential = default(CosmosCredential);

        if (!string.IsNullOrEmpty(cosmosConnectionString))
        {
            cosmosCredential = new(cosmosConnectionString);
        }
        else if (!string.IsNullOrEmpty(cosmosAccountEndpoint) && !string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
        {
            cosmosCredential = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken);
        }
        else
        {
            throw new InvalidOperationException("The Azure Cosmos DB authentication information is not provided");
        }

        return _service.DeployPackageAsync(packagePath, cosmosCredential, cancellationToken);
    }
}
