// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppCheckpointCommand : Command
{
    public static readonly Argument<string> PackageArgument = new("package", "The path to the database package or packages for deployment to the Azure Cosmos DB account");
    public static readonly Argument<string> RollbackPackageArgument = new("rollback-package", "The path to a resulting database package with rollback operations");
    public static readonly Option<Uri> EndpointOption = new("--endpoint", "The address of the Azure Cosmos DB account");
    public static readonly Option<string> KeyOption = new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "The connection string for the Azure Cosmos DB account");

    public AppCheckpointCommand()
        : base("checkpoint", "Creates a database package with rollback operations")
    {
    }
}
