// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class SnapshotCommand : Command
{
    public readonly Argument<string> ProfileArgument = new("profile")
    {
        Description = "The path to the deployment profile or profiles that specify which documents to take snapshots of",
    };

    public readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The path to a resulting database package with import operations",
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

    public SnapshotCommand()
        : base("snapshot", "Creates a database package from the Azure Cosmos DB account")
    {
        Arguments.Add(ProfileArgument);
        Arguments.Add(PackageArgument.AsOutputFile());
        Options.Add(EndpointOption.AsHttpsUri());
        Options.Add(KeyOption);
        Options.Add(ConnectionStringOption);
    }
}
