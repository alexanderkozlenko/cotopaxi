// (c) Oleksandr Kozlenko. Licensed under the MIT license.

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal static class CosmosAuthInfoFactory
{
    public static CosmosAuthInfo CreateAuthInfo(Uri? accountEndpoint, string? authKeyOrResourceToken, string? connectionString)
    {
        var authKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
        var connectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");

        if (Uri.TryCreate(Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT"), UriKind.Absolute, out var accountEndpointVariable))
        {
            if (accountEndpointVariable.Scheme != Uri.UriSchemeHttps)
            {
                accountEndpointVariable = null;
            }
        }

        if (accountEndpoint is not null)
        {
            if (!string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                return new(accountEndpoint, authKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(authKeyOrResourceTokenVariable))
            {
                return new(accountEndpoint, authKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(connectionString))
        {
            return new(connectionString);
        }
        else if (accountEndpointVariable is not null)
        {
            if (!string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                return new(accountEndpointVariable, authKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(authKeyOrResourceTokenVariable))
            {
                return new(accountEndpointVariable, authKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(connectionStringVariable))
        {
            return new(connectionStringVariable);
        }

        throw new InvalidOperationException("Azure Cosmos DB authentication information is not provided");
    }
}
