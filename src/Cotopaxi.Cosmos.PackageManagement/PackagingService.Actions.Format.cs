// (c) Oleksandr Kozlenko. Licensed under the MIT license.

#pragma warning disable CA1848

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed partial class PackagingService
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
                _logger.LogWarning("Formatting {SourcePath} - SKIPPED", sourcePath);

                continue;
            }

            foreach (var document in documents)
            {
                if (document is null)
                {
                    continue;
                }

                CosmosResource.RemoveSystemProperties(document);

                if (document.TryGetPropertyValue("id", out var propertyValueNode))
                {
                    document.Remove("id");
                    document.Insert(0, "id", propertyValueNode);
                }
            }

            using (var sourceStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(sourceStream, documents, jsonSerializerOptionsFormat, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Formatting {SourcePath} - OK", sourcePath);
        }

        return true;
    }
}
