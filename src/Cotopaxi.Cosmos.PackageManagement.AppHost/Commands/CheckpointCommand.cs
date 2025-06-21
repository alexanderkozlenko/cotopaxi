// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class CheckpointCommand : Command
{
    public readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The path to the database package or packages for deployment to the Azure Cosmos DB account",
    };

    public readonly Argument<string> RollbackPackageArgument = new("rollback-package")
    {
        Description = "The path to a resulting database package with rollback operations",
    };

    public readonly Option<Uri> EndpointOption = new("--endpoint")
    {
        Description = "The address of the Azure Cosmos DB account",
    };

    public readonly Option<string> KeyOption = new("--key")
    {
        Description = "The account key or resource token for the Azure Cosmos DB account",
    };

    public readonly Option<string> ConnectionStringOption = new("--connection-string")
    {
        Description = "The connection string for the Azure Cosmos DB account",
    };

    public CheckpointCommand()
        : base("checkpoint", "Creates a database package with rollback operations")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(RollbackPackageArgument.AsOutputFile());
        Options.Add(EndpointOption.AsHttpsUri());
        Options.Add(KeyOption);
        Options.Add(ConnectionStringOption);
    }
}
