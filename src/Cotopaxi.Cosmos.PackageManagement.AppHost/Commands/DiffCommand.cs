// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;
using Cotopaxi.Cosmos.PackageManagement.AppHost.Components;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class DiffCommand : Command
{
    public readonly Argument<string> Package1Argument = new("package1")
    {
        Description = "The path to the database package to compare",
    };

    public readonly Argument<string> Package2Argument = new("package2")
    {
        Description = "The path to the database package to compare with",
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
        Description = "The path to the deployment profile to generate based on new and modified documents",
    };

    public readonly Option<bool> ExitCodeOption = new("--exit-code")
    {
        Description = "Instruct the program to exit with 1 if there were differences and 0 otherwise",
    };

    public DiffCommand()
        : base("diff", "Shows differences between database packages")
    {
        Arguments.Add(Package1Argument.AsInputFile());
        Arguments.Add(Package2Argument.AsInputFile());
        Options.Add(EndpointOption.AsHttpsUri());
        Options.Add(KeyOption);
        Options.Add(ConnectionStringOption);
        Options.Add(ProfileOption.AsOutputFile());
        Options.Add(ExitCodeOption);
    }
}
