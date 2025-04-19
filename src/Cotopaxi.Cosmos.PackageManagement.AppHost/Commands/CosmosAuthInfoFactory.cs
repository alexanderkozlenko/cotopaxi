// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal static class CosmosAuthInfoFactory
{
    public static bool TryGetCreateCosmosAuthInfo(Uri? accountEndpoint, string? authKeyOrResourceToken, string? connectionString, [NotNullWhen(true)] out CosmosAuthInfo? value)
    {
        var authKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
        var connectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");

        Uri.TryCreate(Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT"), UriKind.Absolute, out var accountEndpointVariable);

        value = null;

        if (accountEndpoint is not null)
        {
            if (!string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                value = new(accountEndpoint, authKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(authKeyOrResourceTokenVariable))
            {
                value = new(accountEndpoint, authKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(connectionString))
        {
            value = new(connectionString);
        }
        else if (accountEndpointVariable is not null)
        {
            if (!string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                value = new(accountEndpointVariable, authKeyOrResourceToken);
            }
            else if (!string.IsNullOrEmpty(authKeyOrResourceTokenVariable))
            {
                value = new(accountEndpointVariable, authKeyOrResourceTokenVariable);
            }
        }
        else if (!string.IsNullOrEmpty(connectionStringVariable))
        {
            value = new(connectionStringVariable);
        }

        return value is not null;
    }
}
