// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommonDataModel.ObjectModel.Storage;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class PackageModelAdapter : StorageAdapterBase
{
    public const string Scheme = "opc";

    private readonly Package _package;
    private readonly CompressionOption _compressionOption;
    private readonly CancellationToken _cancellationToken;

    public PackageModelAdapter(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        Debug.Assert(package is not null);

        _package = package;
        _compressionOption = compressionOption;
        _cancellationToken = cancellationToken;
    }

    public override bool CanRead()
    {
        return true;
    }

    public override bool CanWrite()
    {
        return true;
    }

    public override string CreateAdapterPath(string corpusPath)
    {
        Debug.Assert(corpusPath is not null);

        var adapterPath = corpusPath;

        if (!adapterPath.StartsWith('/'))
        {
            adapterPath = '/' + adapterPath;
        }

        return adapterPath;
    }

    public override string CreateCorpusPath(string adapterPath)
    {
        Debug.Assert(adapterPath is not null);

        var corpusPath = adapterPath;

        if (!corpusPath.StartsWith('/'))
        {
            corpusPath = '/' + corpusPath;
        }

        return corpusPath;
    }

    public override async Task<string> ReadAsync(string corpusPath)
    {
        Debug.Assert(corpusPath is not null);

        var packagePartPath = CreateAdapterPath(corpusPath);
        var packagePartUri = new Uri(packagePartPath, UriKind.Relative);
        var packagePart = _package.GetPart(packagePartUri);

        using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
        {
            using (var packagePartReader = new StreamReader(packagePartStream))
            {
                return await packagePartReader.ReadToEndAsync(_cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public override async Task WriteAsync(string corpusPath, string data)
    {
        Debug.Assert(corpusPath is not null);
        Debug.Assert(data is not null);

        var packagePartPath = CreateAdapterPath(corpusPath);
        var packagePartUri = new Uri(packagePartPath, UriKind.Relative);
        var packagePart = _package.CreatePart(packagePartUri, "application/json", _compressionOption);

        // Additional JSON serialization enforces formatting without indentation

        var dataNode = JsonSerializer.Deserialize<JsonNode>(data, JsonSerializerOptions.Default);

        using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(packagePartStream, dataNode, JsonSerializerOptions.Default, _cancellationToken).ConfigureAwait(false);
        }
    }
}
