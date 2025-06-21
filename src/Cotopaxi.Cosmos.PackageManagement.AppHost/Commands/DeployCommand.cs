// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class DeployCommand : Command
{
    public readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The path to the database package or packages to deploy to the Azure Cosmos DB account",
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

    public readonly Option<string> ProfileOption = new("--profile")
    {
        Description = "The path to the deployment profile or profiles that specify documents eligible for updates",
    };

    public readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = "Show which operations would be executed instead of actually executing them",
    };

    public DeployCommand()
        : base("deploy", "Deploys the database packages to the Azure Cosmos DB account")
    {
        Arguments.Add(PackageArgument);
        Options.Add(EndpointOption.AsHttpsUri());
        Options.Add(KeyOption);
        Options.Add(ConnectionStringOption);
        Options.Add(ProfileOption.AsOutputFile());
        Options.Add(DryRunOption);
    }
}
