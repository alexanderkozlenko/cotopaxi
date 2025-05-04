// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.CommonDataModel.ObjectModel.Storage;

namespace Cotopaxi.Cosmos.Packaging;

internal abstract class StorageAdapter : StorageAdapterBase
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    public StorageAdapter(CancellationTokenSource cancellationTokenSource)
    {
        Debug.Assert(cancellationTokenSource is not null);

        _cancellationTokenSource = cancellationTokenSource;
    }

    public override string CreateAdapterPath(string corpusPath)
    {
        Debug.Assert(corpusPath is not null);

        return corpusPath.StartsWith('/') ? corpusPath : "/" + corpusPath;
    }

    public override string CreateCorpusPath(string adapterPath)
    {
        Debug.Assert(adapterPath is not null);

        return adapterPath.StartsWith('/') ? adapterPath : "/" + adapterPath;
    }

    public sealed override async Task<string> ReadAsync(string corpusPath)
    {
        Debug.Assert(corpusPath is not null);

        var cancellationToken = _cancellationTokenSource.Token;
        var adapterPath = CreateAdapterPath(corpusPath);

        using (var document = await ReadAsync(adapterPath, cancellationToken).ConfigureAwait(false)!)
        {
            return document.RootElement.GetRawText();
        }
    }

    public sealed override async Task WriteAsync(string corpusPath, string data)
    {
        Debug.Assert(corpusPath is not null);
        Debug.Assert(data is not null);

        var cancellationToken = _cancellationTokenSource.Token;
        var adapterPath = CreateAdapterPath(corpusPath);

        using (var document = JsonDocument.Parse(data))
        {
            await WriteAsync(adapterPath, document, cancellationToken).ConfigureAwait(false);
        }
    }

    public abstract Task<JsonDocument> ReadAsync(string adapterPath, CancellationToken cancellationToken);

    public abstract Task WriteAsync(string adapterPath, JsonDocument document, CancellationToken cancellationToken);
}
