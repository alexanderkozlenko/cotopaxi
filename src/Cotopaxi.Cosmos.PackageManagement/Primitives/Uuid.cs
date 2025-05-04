// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Hashing;
using System.Text;

namespace Cotopaxi.Cosmos.PackageManagement.Primitives;

public static class Uuid
{
    public static Guid CreateVersion8(string source)
    {
        Debug.Assert(source is not null);

        var hash = XxHash128.Hash(Encoding.Unicode.GetBytes(source));

        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new(hash);
    }
}
