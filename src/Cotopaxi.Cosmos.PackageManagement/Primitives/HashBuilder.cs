// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Cotopaxi.Cosmos.PackageManagement.Primitives;

internal sealed class HashBuilder : IDisposable
{
    private readonly IncrementalHash _hash;
    private readonly Encoding _encoding;

    public HashBuilder(string hashAlgorithmName, Encoding encoding)
    {
        Debug.Assert(hashAlgorithmName is not null);
        Debug.Assert(encoding is not null);

        _hash = IncrementalHash.CreateHash(new(hashAlgorithmName));
        _encoding = encoding;
    }

    public void Dispose()
    {
        _hash.Dispose();
    }

    public void Append(ReadOnlySpan<char> value)
    {
        var byteCount = _encoding.GetByteCount(value);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            var byteSpan = byteBuffer.AsSpan(0, byteCount);

            _encoding.GetBytes(value, byteSpan);
            _hash.AppendData(byteSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }

    public string ToHashString()
    {
        var byteCount = _hash.HashLengthInBytes;
        var byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            var byteSpan = byteBuffer.AsSpan(0, byteCount);

            _hash.GetCurrentHash(byteSpan);

            return Convert.ToHexStringLower(byteSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }
}
