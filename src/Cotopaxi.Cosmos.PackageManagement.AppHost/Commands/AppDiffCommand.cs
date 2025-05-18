// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDiffCommand : Command
{
    public readonly Argument<string> Package1Argument =
        new("package1", "The path to the database package to compare");
    public readonly Argument<string> Package2Argument =
        new("package2", "The path to the database package to compare with");
    public readonly Option<Uri> EndpointOption =
        new("--endpoint", "The address of the Azure Cosmos DB account");
    public readonly Option<string> KeyOption =
        new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public readonly Option<string> ConnectionStringOption =
        new("--connection-string", "The connection string for the Azure Cosmos DB account");
    public readonly Option<string> ProfileOption =
        new("--profile", "The path to the deployment profile to generate based on new and modified documents");
    public readonly Option<bool> ExitCodeOption =
        new("--exit-code", "Instruct the program to exit with 1 if there were differences and 0 otherwise");

    public AppDiffCommand()
        : base("diff", "Shows differences between database packages")
    {
        Package1Argument.AddValidationAsInputFile();
        Package2Argument.AddValidationAsInputFile();
        EndpointOption.AddValidationAsHttpsUri();
        ProfileOption.AddValidationAsOutputFile();

        AddArgument(Package1Argument);
        AddArgument(Package2Argument);
        AddOption(EndpointOption);
        AddOption(KeyOption);
        AddOption(ConnectionStringOption);
        AddOption(ProfileOption);
        AddOption(ExitCodeOption);
    }
}
