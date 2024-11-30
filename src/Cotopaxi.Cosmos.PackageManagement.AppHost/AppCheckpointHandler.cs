﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppCheckpointHandler : HostCommandHandler
{
    private readonly PackagingService _service;

    public AppCheckpointHandler(PackagingService service)
    {
        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        var cosmosAccountEndpoint = commandResult.GetValueForOption(AppCheckpointCommand.EndpointOption);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(AppCheckpointCommand.KeyOption);
        var cosmosAuthKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
        var cosmosConnectionString = commandResult.GetValueForOption(AppCheckpointCommand.ConnectionStringOption);
        var cosmosConnectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");

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
            if (!string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
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

        var sourcePackagePathPattern = commandResult.GetValueForArgument(AppCheckpointCommand.SourcePackageArgument);
        var sourcePackagePaths = GetFiles(Environment.CurrentDirectory, sourcePackagePathPattern);
        var revertPackagePath = commandResult.GetValueForArgument(AppCheckpointCommand.RevertPackageArgument);

        return _service.CreateCheckpointPackagesAsync(sourcePackagePaths, revertPackagePath, cosmosCredential, cancellationToken);
    }
}