// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppSnapshotCommand : Command
{
    public readonly Argument<string> ProfileArgument =
        new("profile", "The path to the deployment profile or profiles that specify which documents to take snapshots of");
    public readonly Argument<string> PackageArgument =
        new("package", "The path to a resulting database package with import operations");
    public readonly Option<Uri> EndpointOption =
        new("--endpoint", "The address of the Azure Cosmos DB account");
    public readonly Option<string> KeyOption =
        new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public readonly Option<string> ConnectionStringOption =
        new("--connection-string", "The connection string for the Azure Cosmos DB account");

    public AppSnapshotCommand()
        : base("snapshot", "Creates a database package from the Azure Cosmos DB account")
    {
        PackageArgument.AddValidationAsOutputFile();
        EndpointOption.AddValidationAsHttpsUri();

        AddArgument(ProfileArgument);
        AddArgument(PackageArgument);
        AddOption(EndpointOption);
        AddOption(KeyOption);
        AddOption(ConnectionStringOption);
    }
}
