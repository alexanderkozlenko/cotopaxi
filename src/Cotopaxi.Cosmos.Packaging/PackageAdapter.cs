// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;
using System.Text.Json;

namespace Cotopaxi.Cosmos.Packaging;

internal sealed class PackageAdapter : StorageAdapter
{
    public const string SchemeName = "opc";

    private readonly Package _package;
    private readonly CompressionOption _compressionOption;

    public PackageAdapter(Package package, CompressionOption compressionOption, CancellationTokenSource cancellationTokenSource)
        : base(cancellationTokenSource)
    {
        Debug.Assert(package is not null);

        _package = package;
        _compressionOption = compressionOption;
    }

    public override bool CanRead()
    {
        return _package.FileOpenAccess.HasFlag(FileAccess.Read);
    }

    public override bool CanWrite()
    {
        return _package.FileOpenAccess.HasFlag(FileAccess.Write);
    }

    public override async Task<JsonDocument> ReadAsync(string adapterPath, CancellationToken cancellationToken)
    {
        Debug.Assert(adapterPath is not null);

        var packagePart = _package.GetPart(new(adapterPath, UriKind.Relative));

        using (var packagePartStream = packagePart.GetStream(FileMode.Open, FileAccess.Read))
        {
            return await JsonDocument.ParseAsync(packagePartStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task WriteAsync(string adapterPath, JsonDocument document, CancellationToken cancellationToken)
    {
        Debug.Assert(adapterPath is not null);
        Debug.Assert(document is not null);

        var packagePart = _package.CreatePart(new(adapterPath, UriKind.Relative), "application/json", _compressionOption);

        using (var packagePartStream = packagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(packagePartStream, document.RootElement, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
