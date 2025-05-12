// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Buffers;
using System.IO.Hashing;
using System.Text;

namespace Cotopaxi.Cosmos.PackageManagement.Primitives;

public static class Uuid
{
    public static Guid CreateVersion8(ReadOnlySpan<char> source)
    {
        var byteCount = Encoding.Unicode.GetByteCount(source);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var hashBuffer = ArrayPool<byte>.Shared.Rent(16);

        try
        {
            var byteSpan = byteBuffer.AsSpan(0, byteCount);
            var hashSpan = hashBuffer.AsSpan(0, 16);

            Encoding.Unicode.GetBytes(source, byteSpan);
            XxHash128.Hash(byteSpan, hashSpan);

            hashSpan[7] = (byte)((hashSpan[7] & 0b00001111) | 0b10000000);
            hashSpan[8] = (byte)((hashSpan[8] & 0b00111111) | 0b10000000);

            return new(hashSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
            ArrayPool<byte>.Shared.Return(hashBuffer);
        }
    }
}
