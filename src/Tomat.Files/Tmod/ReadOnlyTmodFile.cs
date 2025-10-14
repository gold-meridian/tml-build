using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;

namespace Tomat.Files.Tmod;

/// <summary>
///     A read-only <c>.tmod</c> file, providing APIs for serialization and
///     deserialization.
/// </summary>
public readonly struct ReadOnlyTmodFile : ITmodFile
{
    private readonly record struct Entry(
        int CompressedLength,
        int UncompressedLength,
        long StreamOffset
    )
    {
        public bool IsCompressed => UncompressedLength != CompressedLength;
    }

    public string ModLoaderVersion
    {
        get => modLoaderVersion;
        set => throw new InvalidOperationException();
    }

    public string ModName
    {
        get => modName;
        set => throw new InvalidOperationException();
    }

    public string ModVersion
    {
        get => modVersion;
        set => throw new InvalidOperationException();
    }

    private readonly Stream seekableStream;
    private readonly Stream readableStream;

    private readonly string modLoaderVersion;
    private readonly string modName;
    private readonly string modVersion;
    private readonly Dictionary<string, Entry> entries;
    private readonly Dictionary<string, byte[]> entryByteCache = [];

    private ReadOnlyTmodFile(
        Stream seekableStream,
        Stream readableStream,
        string modLoaderVersion,
        string modName,
        string modVersion,
        Dictionary<string, Entry> entries
    )
    {
        this.seekableStream = seekableStream;
        this.readableStream = readableStream;
        this.modLoaderVersion = modLoaderVersion;
        this.modName = modName;
        this.modVersion = modVersion;
        this.entries = entries;
    }

    public bool HasFile(string fileName)
    {
        return entries.ContainsKey(fileName);
    }

    public byte[]? GetFile(string fileName)
    {
        if (!HasFile(fileName))
        {
            return null;
        }

        if (entryByteCache.TryGetValue(fileName, out var bytes))
        {
            return bytes;
        }

        var entry = entries[fileName];
        {
            seekableStream.Position = entry.StreamOffset;
        }

        bytes = new byte[entry.UncompressedLength];

        if (entry.IsCompressed)
        {
            var compressedBytes = new byte[entry.CompressedLength];
            if (readableStream.Read(compressedBytes, 0, compressedBytes.Length) != compressedBytes.Length)
            {
                throw new IOException($"Failed to read compressed bytes for entry: {fileName}");
            }

            if (!Decompress(compressedBytes, bytes))
            {
                throw new IOException($"Failed to decompress bytes for entry: {fileName}");
            }

            return entryByteCache[fileName] = bytes;
        }

        if (readableStream.Read(bytes, 0, entry.UncompressedLength) != entry.UncompressedLength)
        {
            throw new IOException($"Failed to read bytes for entry: {fileName}");
        }

        return entryByteCache[fileName] = bytes;
    }

    public bool TryGetFile(string fileName, [NotNullWhen(returnValue: true)] out byte[]? fileBytes)
    {
        fileBytes = GetFile(fileName);
        return fileBytes is not null;
    }

    public void AddFile(string fileName, byte[] fileBytes)
    {
        throw new InvalidOperationException();
    }

    public IEnumerable<string> GetEntries()
    {
        return entries.Keys;
    }

    public void Dispose()
    {
        // seekableStream.Dispose();
        readableStream.Dispose();
    }

#region Compression
    private static bool Decompress(byte[] compressedBytes, byte[] bytes)
    {
        using var ms = new MemoryStream(compressedBytes);
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);

        return ds.Read(bytes, 0, bytes.Length) == bytes.Length;
    }

    // https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.IO.Compression/src/System/IO/Compression/DeflateZLib/DeflateStream.cs#L69
    // CompressionMode.Compress corresponds to CompressionLevel.Optimal by
    // default.
    private static byte[] Compress(byte[] bytes, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var ms = new MemoryStream(bytes.Length);
        using var ds = new DeflateStream(ms, level);
        ds.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }
#endregion
}
