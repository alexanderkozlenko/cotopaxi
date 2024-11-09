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
        var cosmosCredential = default(CosmosCredential);

        if (!commandResult.GetValueForOption(PackageDeployingCommand.DryRun))
        {
            var cosmosAccountEndpoint = commandResult.GetValueForOption(PackageDeployingCommand.EndpointOption);
            var cosmosAuthKeyOrResourceToken = commandResult.GetValueForOption(PackageDeployingCommand.KeyOption);
            var cosmosAuthKeyOrResourceTokenVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY");
            var cosmosConnectionString = commandResult.GetValueForOption(PackageDeployingCommand.ConnectionStringOption);
            var cosmosConnectionStringVariable = Environment.GetEnvironmentVariable("AZURE_COSMOS_CONNECTION_STRING");

            Uri.TryCreate(Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT"), UriKind.Absolute, out var cosmosAccountEndpointVariable);

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
        }

        var packageFilePattern = commandResult.GetValueForArgument(PackageDeployingCommand.PackageArgument);
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

        var matcher = new Matcher().AddInclude(pattern);
        var match = matcher.Execute(new DirectoryInfoWrapper(new(directoryPath)));

        return match.Files
            .Select(x => new FileInfo(Path.GetFullPath(Path.Combine(directoryPath, x.Path))))
            .OrderBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
