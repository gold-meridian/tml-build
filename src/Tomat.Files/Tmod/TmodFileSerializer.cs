using System.IO;
using System.IO.Compression;

namespace Tomat.Files.Tmod;

public static class TmodFileSerializer
{
#region Compression
    public static bool Decompress(byte[] compressedBytes, byte[] bytes)
    {
        using var ms = new MemoryStream(compressedBytes);
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);

        return ds.Read(bytes, 0, bytes.Length) == bytes.Length;
    }

    // https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.IO.Compression/src/System/IO/Compression/DeflateZLib/DeflateStream.cs#L69
    // CompressionMode.Compress corresponds to CompressionLevel.Optimal by
    // default.
    public static byte[] Compress(byte[] bytes, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var ms = new MemoryStream(bytes.Length);
        using var ds = new DeflateStream(ms, level);
        ds.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }
#endregion
}
