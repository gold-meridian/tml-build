using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Tomat.Files.Tmod;

/// <summary>
///     A mutable <c>.tmod</c> file, convertible to a
///     <see cref="ReadOnlyTmodFile"/> and designed to be used in constructing
///     new <c>.tmod</c> files.
/// </summary>
public sealed class TmodFile
{
    public readonly record struct FileOptions(
        uint MinCompressionSize,
        float CompressionTradeoff,
        CompressionLevel CompressionLevel
    )
    {
        public static readonly FileOptions DEFAULT_COMPRESSION = new(
            MinCompressionSize: 1 << 10, // 1 KiB
            CompressionTradeoff: 0.9f,
            CompressionLevel: CompressionLevel.Optimal
        );
    }

    public readonly record struct Entry(
        byte[] Data,
        int UncompressedLength
    )
    {
        public int CompressedLength => Data.Length;
    }

    public required string ModLoaderVersion { get; set; }

    public required string ModName { get; set; }

    public required string ModVersion { get; set; }

    public Dictionary<string, Entry> Files { get; } = [];

    public bool HasFile(string fileName)
    {
        fileName = SanitizePath(fileName);

        return Files.ContainsKey(fileName);
    }

    public void AddFile(
        string fileName,
        byte[] fileBytes,
        FileOptions? fileOptions = null
    )
    {
        fileName = SanitizePath(fileName);
        fileOptions ??= FileOptions.DEFAULT_COMPRESSION;

        var fileSize = fileBytes.Length;
        if (fileSize > fileOptions.Value.MinCompressionSize && ShouldCompress(fileName))
        {
            var compressedBytes = TmodFileSerializer.Compress(fileBytes, fileOptions.Value.CompressionLevel);
            if (compressedBytes.Length < fileSize * fileOptions.Value.CompressionTradeoff)
            {
                fileBytes = compressedBytes;
            }
        }

        // TODO: Check if it's overwriting?
        Files[fileName] = new Entry(fileBytes, fileSize);

        return;

        static bool ShouldCompress(string fileName)
        {
            // We can definitely add more, tML is lacking.  These are for files
            // that include compression in their spec.
            return !fileName.EndsWith(".png")
                && !fileName.EndsWith(".mp3")
                && !fileName.EndsWith(".ogg");
        }
    }

    public ReadOnlyTmodFile AsReadOnly()
    {
        using var ms = new MemoryStream();
        TmodFileSerializer.Write(ms, this);
        return TmodFileSerializer.Read(ms);
    }

    internal static string SanitizePath(string path)
    {
        // - Trim whitespace,
        // - normalize path separators to '/',
        // - trim leading and trailing path separators.
        return path.Trim().Replace('\\', '/').Trim('/');
    }
}
