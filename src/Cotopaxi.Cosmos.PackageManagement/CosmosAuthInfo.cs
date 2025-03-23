// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class CosmosAuthInfo
{
    public CosmosAuthInfo(string connectionString)
    {
        Debug.Assert(connectionString is { Length: > 0 });

        ConnectionString = connectionString;
        IsConnectionString = true;
    }

    public CosmosAuthInfo(Uri accountEndpoint, string authKeyOrResourceToken)
    {
        Debug.Assert(accountEndpoint is not null);
        Debug.Assert(authKeyOrResourceToken is { Length: > 0 });

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

    public Uri? AccountEndpoint
    {
        get;
    }

    public string? AuthKeyOrResourceToken
    {
        get;
    }
}
