// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class VersionBuilder : IDisposable
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

    public void Dispose()
    {
        _hash.Dispose();
    }

    public void Append(ReadOnlySpan<char> value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            var byteSpan = byteBuffer.AsSpan(0, byteCount);

            Encoding.UTF8.GetBytes(value, byteSpan);

            _hash.AppendData(byteSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }

    public string ToVersion()
    {
        var bytes = (stackalloc byte[SHA1.HashSizeInBytes]);

        _hash.GetCurrentHash(bytes);

        return Convert.ToHexStringLower(bytes);
    }
}
