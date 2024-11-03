// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackageDeployingCommand : Command
{
    public static readonly Argument<string> PackageArgument = new("package", "Specifies the package to deploy");
    public static readonly Option<Uri> EndpointOption = new("--endpoint", "Specifies the Azure Cosmos DB endpoint");
    public static readonly Option<string> KeyOption = new("--key", "Specifies the Azure Cosmos DB account key or resource token");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "Specifies the Azure Cosmos DB connection string");

    public PackageDeployingCommand()
        : base("deploy", "Deploys a data package to an Azure Cosmos DB instance")
    {
    }
}
