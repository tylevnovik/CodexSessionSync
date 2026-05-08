using System;
using System.Security.Cryptography;
using System.Text;

namespace CodexSessionSync.Core;

public static class UuidV5
{
    public static readonly Guid SyncNamespace = new("7c3bd33f-77a8-4b6f-b91e-6f4236f26b4e");

    public static Guid Create(Guid namespaceId, string name)
    {
        using var sha1 = SHA1.Create();
        var nsBytes = GuidToBigEndian(namespaceId);
        var nameBytes = Encoding.UTF8.GetBytes(name);

        sha1.TransformBlock(nsBytes, 0, 16, null, 0);
        sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
        var hash = sha1.Hash!;

        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return GuidFromBigEndian(guidBytes);
    }

    private static byte[] GuidToBigEndian(Guid guid)
    {
        var b = guid.ToByteArray();
        return new[]
        {
            b[3], b[2], b[1], b[0],
            b[5], b[4],
            b[7], b[6],
            b[8], b[9], b[10], b[11],
            b[12], b[13], b[14], b[15]
        };
    }

    private static Guid GuidFromBigEndian(byte[] b)
    {
        var net = new[]
        {
            b[3], b[2], b[1], b[0],
            b[5], b[4],
            b[7], b[6],
            b[8], b[9], b[10], b[11],
            b[12], b[13], b[14], b[15]
        };
        return new Guid(net);
    }
}
