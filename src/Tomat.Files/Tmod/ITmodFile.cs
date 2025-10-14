using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Tomat.Files.Tmod;

/// <summary>
///     A <c>.tmod</c> file archive.
/// </summary>
public interface ITmodFile : IDisposable
{
    string ModLoaderVersion { get; set; }

    string ModName { get; set; }

    string ModVersion { get; set; }

    bool HasFile(string fileName);

    byte[]? GetFile(string fileName);

    bool TryGetFile(
        string fileName,
        [NotNullWhen(returnValue: true)] out byte[]? fileBytes
    );

    void AddFile(string fileName, byte[] fileBytes);

    IEnumerable<string> GetEntries();
}
