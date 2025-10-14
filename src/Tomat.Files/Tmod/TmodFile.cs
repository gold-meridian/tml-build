using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Tomat.Files.Tmod;

/// <summary>
///     A mutable <c>.tmod</c> file, convertible to a
///     <see cref="ReadOnlyTmodFile"/> and designed to be used in constructing
///     new <c>.tmod</c> files.
/// </summary>
public sealed class TmodFile : ITmodFile
{
    public readonly record struct FileOptions(
        uint MinCompressionSize = 1 << 10, // 1 KiB
        float CompressionTradeoff = 0.9f
    )
    {
        public static readonly FileOptions DEFAULT = new();
    }

    public required string ModLoaderVersion { get; set; }

    public required string ModName { get; set; }

    public required string ModVersion { get; set; }

    private readonly Dictionary<string, byte[]> files = [];

    public bool HasFile(string fileName)
    {
        return files.ContainsKey(fileName);
    }

    public byte[]? GetFile(string fileName)
    {
        return files.TryGetValue(fileName, out var bytes) ? bytes : null;
    }

    public bool TryGetFile(string fileName, [NotNullWhen(true)] out byte[]? fileBytes)
    {
        return files.TryGetValue(fileName, out fileBytes);
    }

    public void AddFile(string fileName, byte[] fileBytes)
    {
        AddFile(fileName, fileBytes)
    }

    public IEnumerable<string> GetEntries()
    {
        throw new NotImplementedException();
    }

    public ReadOnlyTmodFile AsReadOnly()
    {
        throw new NotImplementedException();
    }

    public void Dispose() { }
}
