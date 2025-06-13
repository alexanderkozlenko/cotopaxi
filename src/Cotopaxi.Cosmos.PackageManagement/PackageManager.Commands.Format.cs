// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackageManager
{
    public async Task<bool> FormatSourcesAsync(IReadOnlyCollection<string> sourcePaths, CancellationToken cancellationToken)
    {
        Debug.Assert(sourcePaths is not null);

        if (sourcePaths.Count == 0)
        {
            return true;
        }

        var jsonSerializerOptions = new JsonSerializerOptions(s_jsonSerializerOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        foreach (var sourcePath in sourcePaths)
        {
            var documents = default(JsonObject?[]);

            try
            {
                await using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(sourceStream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                }
            }
            catch (JsonException)
            {
                continue;
            }

            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];

                if (document is null)
                {
                    continue;
                }

                CosmosDocument.PruneSystemProperties(document);
                CosmosDocument.OrderSystemProperties(document);

                _logger.LogInformation("{SourcePath}:$[{DocumentIndex}]: OK", sourcePath, i);
            }

            await using (var sourceStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(sourceStream, documents, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }
}
