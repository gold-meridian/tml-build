using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Tomat.Files.Tmod;

/// <summary>
///     A read-only <c>.tmod</c> file, providing APIs for serialization and
///     deserialization.
/// </summary>
public readonly struct ReadOnlyTmodFile : IDisposable
{
    internal readonly record struct Entry(
        int CompressedLength,
        int UncompressedLength,
        long StreamOffset
    )
    {
        public bool IsCompressed => UncompressedLength != CompressedLength;
    }

    public string ModLoaderVersion { get; init; }

    public string ModName { get; init; }

    public string ModVersion { get; init; }

    private readonly Stream seekableStream;
    private readonly Stream readableStream;

    private readonly Dictionary<string, Entry> entries;
    private readonly Dictionary<string, byte[]> entryByteCache = [];

    internal ReadOnlyTmodFile(
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
        ModLoaderVersion = modLoaderVersion;
        ModName = modName;
        ModVersion = modVersion;
        this.entries = entries;
    }

    public bool HasFile(string fileName)
    {
        fileName = TmodFile.SanitizePath(fileName);

        return entries.ContainsKey(fileName);
    }

    public byte[]? GetFile(string fileName)
    {
        fileName = TmodFile.SanitizePath(fileName);

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

            return entryByteCache[fileName] = TmodFileSerializer.Decompress(compressedBytes);
        }

        if (readableStream.Read(bytes, 0, entry.UncompressedLength) != entry.UncompressedLength)
        {
            throw new IOException($"Failed to read bytes for entry: {fileName}");
        }

        return entryByteCache[fileName] = bytes;
    }

    public bool TryGetFile(string fileName, [NotNullWhen(returnValue: true)] out byte[]? fileBytes)
    {
        fileName = TmodFile.SanitizePath(fileName);

        fileBytes = GetFile(fileName);
        return fileBytes is not null;
    }

    public IEnumerable<string> GetEntries()
    {
        return entries.Keys;
    }

    public TmodFile AsMutable()
    {
        var file = new TmodFile
        {
            ModLoaderVersion = ModLoaderVersion,
            ModName = ModName,
            ModVersion = ModVersion,
        };

        foreach (var kvp in entries)
        {
            var path = kvp.Key;
            var entry = kvp.Value;

            if (!TryGetFile(path, out var bytes))
            {
                // TODO
                throw new Exception();
            }

            // Directly copy the data over, skip AddFile.
            file.Files[path] = new TmodFile.Entry(bytes, entry.UncompressedLength);
        }

        return file;
    }

    public void Dispose()
    {
        // seekableStream.Dispose();
        readableStream.Dispose();
    }
}
