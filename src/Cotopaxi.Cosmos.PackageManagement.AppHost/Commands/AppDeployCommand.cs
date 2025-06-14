﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDeployCommand : Command
{
    public readonly Argument<string> PackageArgument =
        new("package", "The path to the database package or packages to deploy to the Azure Cosmos DB account");
    public readonly Option<Uri> EndpointOption =
        new("--endpoint", "The address of the Azure Cosmos DB account");
    public readonly Option<string> KeyOption =
        new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public readonly Option<string> ConnectionStringOption =
        new("--connection-string", "The connection string for the Azure Cosmos DB account");
    public readonly Option<string> ProfileOption =
        new("--profile", "The path to the deployment profile or profiles that specify documents eligible for updates");
    public readonly Option<bool> DryRunOption =
        new("--dry-run", "Show which operations would be executed instead of actually executing them");

    public AppDeployCommand()
        : base("deploy", "Deploys the database packages to the Azure Cosmos DB account")
    {
        EndpointOption.AddValidationAsHttpsUri();
        ProfileOption.AddValidationAsOutputFile();

        AddArgument(PackageArgument);
        AddOption(EndpointOption);
        AddOption(KeyOption);
        AddOption(ConnectionStringOption);
        AddOption(ProfileOption);
        AddOption(DryRunOption);
    }
}
