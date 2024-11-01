// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackageDeployingCommand : Command
{
    public static readonly Argument<string> PackageArgument = new("package", "Specifies the package to deploy");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "Specifies the connection string (defaults to COSMOS_CONNECTION_STRING environment variable)");
    public static readonly Option<string> EndpointOption = new("--endpoint", "Specifies the endpoint (defaults to COSMOS_ENDPOINT environment variable)");
    public static readonly Option<string> KeyOption = new("--key", "Specifies the account key or resource token (defaults to COSMOS_KEY environment variable)");

    public PackageDeployingCommand()
        : base("deploy", "Deploys a data package to an Azure Cosmos DB instance")
    {
    }
}
