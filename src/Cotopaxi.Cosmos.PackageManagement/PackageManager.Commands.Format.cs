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

        var jsonSerializerOptionsFormat = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        foreach (var sourcePath in sourcePaths)
        {
            _logger.LogInformation("Formatting {SourcePath}", sourcePath);

            var documents = default(JsonObject?[]);

            try
            {
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    documents = await JsonSerializer.DeserializeAsync<JsonObject?[]>(sourceStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
                }
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var document in documents)
            {
                if (document is null)
                {
                    continue;
                }

                CosmosResource.FormatDocument(document);
            }

            using (var sourceStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(sourceStream, documents, jsonSerializerOptionsFormat, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }
}
