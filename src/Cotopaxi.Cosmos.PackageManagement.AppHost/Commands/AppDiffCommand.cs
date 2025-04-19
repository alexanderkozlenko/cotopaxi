// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost.Commands;

internal sealed class AppDiffCommand : Command
{
    public static readonly Argument<string> Package1Argument = new("package1", "The database package to compare");
    public static readonly Argument<string> Package2Argument = new("package2", "The database package to compare with");
    public static readonly Option<Uri> EndpointOption = new("--endpoint", "The address of the Azure Cosmos DB account");
    public static readonly Option<string> KeyOption = new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "The connection string for the Azure Cosmos DB account");
    public static readonly Option<bool> ExitCodeOption = new("--exit-code", "Make the program exit with 1 if there were differences and 0 otherwise");

    public AppDiffCommand()
        : base("diff", "Shows changes between database packages")
    {
    }
}
