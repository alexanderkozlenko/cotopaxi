// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.CommandLine.Parsing;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Cotopaxi.Cosmos.PackageManagement.AppHost;

internal sealed class PackageDeployingHandler : HostCommandHandler
{
    private readonly PackageService _service;

    public PackageDeployingHandler(PackageService service)
    {
        _service = service;
    }

    protected override Task<bool> InvokeAsync(CommandResult commandResult, CancellationToken cancellationToken)
    {
        var packageFilePattern = commandResult.GetValueForArgument(PackageDeployingCommand.PackageArgument);
        var cosmosConnectionString = commandResult.GetValueForOption(PackageDeployingCommand.ConnectionStringOption);
        var cosmosConnectionStringVariable = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
        var cosmosAccountEndpoint = commandResult.GetValueForOption(PackageDeployingCommand.EndpointOption);
        var cosmosAccountEndpointVariable = default(Uri);
        var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(PackageDeployingCommand.KeyOption);
        var cosmosAuthKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("COSMOS_KEY");

        Uri.TryCreate(Environment.GetEnvironmentVariable("COSMOS_ENDPOINT"), UriKind.Absolute, out cosmosAccountEndpointVariable);

        var cosmosCredential = default(CosmosCredential);

        if ((cosmosAccountEndpoint is not null) && !string.IsNullOrEmpty(cosmosAuthKeyOrResourceToken))
        {
            cosmosCredential = new(cosmosAccountEndpoint, cosmosAuthKeyOrResourceToken);
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionString))
        {
            cosmosCredential = new(cosmosConnectionString);
        }
        else if ((cosmosAccountEndpointVariable is not null) && !string.IsNullOrEmpty(cosmosAuthKeyOrResourceTokenVariable))
        {
            cosmosCredential = new(cosmosAccountEndpointVariable, cosmosAuthKeyOrResourceTokenVariable);
        }
        else if (!string.IsNullOrEmpty(cosmosConnectionStringVariable))
        {
            cosmosCredential = new(cosmosConnectionStringVariable);
        }
        else
        {
            throw new InvalidOperationException("The Azure Cosmos DB authentication information is not provided");
        }

        var packageFiles = FindMatchingFiles(Environment.CurrentDirectory, packageFilePattern);

        return _service.DeployPackageAsync(packageFiles, cosmosCredential, cancellationToken);
    }

    private static FileInfo[] FindMatchingFiles(string directoryPath, string pattern)
    {
        if (Path.IsPathRooted(pattern))
        {
            directoryPath = Path.GetPathRoot(pattern)!;
            pattern = Path.GetRelativePath(directoryPath, pattern);
        }

        var directory = new DirectoryInfo(directoryPath);
        var matcher = new Matcher(StringComparison.Ordinal).AddInclude(pattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(directory));

        return match.Files.Select(x => new FileInfo(Path.GetFullPath(Path.Combine(directoryPath, x.Path)))).ToArray();
    }
}
