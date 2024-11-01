// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class CosmosCredential
{
    public CosmosCredential(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        ConnectionString = connectionString;
        IsConnectionString = true;
    }

    public CosmosCredential(string accountEndpoint, string authKeyOrResourceToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(authKeyOrResourceToken);

        AccountEndpoint = accountEndpoint;
        AuthKeyOrResourceToken = authKeyOrResourceToken;
    }

    [MemberNotNullWhen(true, nameof(ConnectionString))]
    [MemberNotNullWhen(false, nameof(AccountEndpoint))]
    [MemberNotNullWhen(false, nameof(AuthKeyOrResourceToken))]
    internal bool IsConnectionString
    {
        get;
    }

    public string? ConnectionString
    {
        get;
    }

    public string? AccountEndpoint
    {
        get;
    }

    public string? AuthKeyOrResourceToken
    {
        get;
    }
}
