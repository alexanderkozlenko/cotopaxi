// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using Cotopaxi.Cosmos.PackageManagement.Contracts;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    private static readonly string s_applicationName = $"cotopaxi/{typeof(PackageManager).Assembly.GetName().Version?.ToString(3)}";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public PackageManager(TimeProvider timeProvider, ILogger<PackageManager> logger)
    {
        Debug.Assert(timeProvider is not null);
        Debug.Assert(logger is not null);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    private static async Task<FrozenSet<PackageDocumentKey>> GetProfileDocumentKeysAsync(IReadOnlyCollection<string> profilePaths, CancellationToken cancellationToken)
    {
        var documentKeys = new HashSet<PackageDocumentKey>();

        foreach (var profilePath in profilePaths)
        {
            var documentKeyNodes = default(PackageDocumentKeyNode?[]);

            await using (var profileStream = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                documentKeyNodes = await JsonSerializer.DeserializeAsync<PackageDocumentKeyNode?[]>(profileStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }

            foreach (var documentKeyNode in documentKeyNodes.Where(static x => x is not null))
            {
                var documentKey = new PackageDocumentKey(
                    documentKeyNode!.DatabaseName.Value,
                    documentKeyNode!.ContainerName.Value,
                    documentKeyNode!.DocumentId.Value,
                    documentKeyNode!.DocumentPartitionKey);

                documentKeys.Add(documentKey);
            }
        }

        return documentKeys.ToFrozenSet();
    }

    private static CosmosClient CreateCosmosClient(CosmosAuthInfo cosmosAuthInfo, Action<CosmosClientOptions>? configure = null)
    {
        var options = new CosmosClientOptions
        {
            ApplicationName = s_applicationName,
            UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Default,
        };

        configure?.Invoke(options);

        return cosmosAuthInfo.IsConnectionString ?
            new CosmosClient(cosmosAuthInfo.ConnectionString, options) :
            new CosmosClient(cosmosAuthInfo.AccountEndpoint.AbsoluteUri, cosmosAuthInfo.AuthKeyOrResourceToken, options);
    }
}
