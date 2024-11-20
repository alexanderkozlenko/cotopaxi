// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class AppCheckpointCommand : Command
{
    public static readonly Argument<string> SourcePackageArgument = new("source-package", "The path to a package or packages for deployment deploy to the Azure Cosmos DB account");
    public static readonly Argument<string> RevertPackageArgument = new("revert-package", "The path to a package that reverts operations from the source package or packages");
    public static readonly Option<Uri> EndpointOption = new("--endpoint", "The address of the Azure Cosmos DB account");
    public static readonly Option<string> KeyOption = new("--key", "The account key or resource token for the Azure Cosmos DB account");
    public static readonly Option<string> ConnectionStringOption = new("--connection-string", "The connection string for the Azure Cosmos DB account");

    public AppCheckpointCommand()
        : base("checkpoint", "Creates a package that reverts operations from the source package or packages")
    {
    }
}
