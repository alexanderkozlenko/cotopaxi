// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackageDeployingCommand : Command
{
    public static readonly Argument<string> PackageArgument = new("package", "The package to deploy to the Azure Cosmos DB account");
    public static readonly Option<Uri> EndpointOption = new("--endpoint", "The address of the Azure Cosmos DB account");
    public static readonly Option<string> KeyOption = new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "The connection string for the Azure Cosmos DB account");
    public static readonly Option<bool> DryRun = new("--dry-run", "Show which operations would be executed, but don't execute them");

    public PackageDeployingCommand()
        : base("deploy", "Deploys a package to an Azure Cosmos DB account")
    {
    }
}
