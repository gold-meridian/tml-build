using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Tomat.Files.Tmod;

public static class TmodFileSerializer
{
    private static readonly Version new_format_milestone = new(0, 11, 0, 0);
    private const int tmod_magic_header = 0x444F4D54; // "TMOD"
    private const int hash_length = 20;
    private const int signature_length = 256;

    private static readonly SHA1 sha1 = SHA1.Create();

    public static ReadOnlyTmodFile Read(string path)
    {
        // ReadOnlyTmodFile disposes of the stream.
        return Read(File.OpenRead(path));
    }

    public static ReadOnlyTmodFile Read(Stream stream)
    {
        var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        try
        {
            if (reader.ReadUInt32() != tmod_magic_header)
            {
                throw new InvalidOperationException("Invalid TMOD header");
            }

            var tmlVersion = reader.ReadString();

            stream.Position += hash_length      // hash
                             + signature_length // signature
                             + sizeof(uint);    // data blob length

            var seekable = stream;
            var isLegacy = Version.Parse(tmlVersion) < new_format_milestone;

            if (isLegacy)
            {
                var ds = new DeflateStream(
                    stream,
                    mode: CompressionMode.Decompress,
                    leaveOpen: false
                );

                reader.Dispose();
                reader = new BinaryReader(ds, Encoding.UTF8, leaveOpen: true);
            }

            var modName = reader.ReadString();
            var modVersion = reader.ReadString();

            var entryCount = reader.ReadInt32();
            var entries = new Dictionary<string, ReadOnlyTmodFile.Entry>(entryCount);

            if (isLegacy)
            {
                for (var i = 0; i < entryCount; i++)
                {
                    var path = reader.ReadString();
                    var length = reader.ReadInt32();

                    entries.Add(
                        path,
                        new ReadOnlyTmodFile.Entry(
                            length,
                            length,
                            seekable.Position
                        )
                    );

                    seekable.Position += length;
                }
            }
            else
            {
                for (var i = 0; i < entryCount; i++)
                {
                    var path = reader.ReadString();
                    var length = reader.ReadInt32();
                    var compressedLength = reader.ReadInt32();

                    entries.Add(
                        path,
                        new ReadOnlyTmodFile.Entry(
                            compressedLength,
                            length,
                            0
                        )
                    );
                }
            }

            foreach (var kvp in entries)
            {
                var path = kvp.Key;
                var entry = kvp.Value;

                Debug.Assert(entry.CompressedLength <= entry.UncompressedLength && entry.CompressedLength > 0);

                entries[path] = entry with
                {
                    StreamOffset = seekable.Position,
                };

                seekable.Position += entry.CompressedLength;
            }

            return new ReadOnlyTmodFile(
                seekable,
                reader.BaseStream,
                tmlVersion,
                modName,
                modVersion,
                entries
            );
        }
        finally
        {
            reader.Dispose();
        }
    }

    public static void Write(string path, TmodFile tmod)
    {
        using var stream = File.OpenWrite(path);
        Write(stream, tmod);
    }

    public static void Write(Stream stream, TmodFile tmod)
    {
        // This also doubles as a guard for if the mod loader version is
        // invalid, I guess.
        if (Version.Parse(tmod.ModLoaderVersion) < new_format_milestone)
        {
            throw new InvalidOperationException($"Cannot serialize outdated TMOD file: {new_format_milestone}");
        }

        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write(tmod_magic_header);
        writer.Write(tmod.ModLoaderVersion);

        // TODO: I forget if I can seek to skip this.
        var hashPos = (int)stream.Position;
        writer.Write(new byte[signature_length + hash_length + sizeof(uint)]);

        var dataPos = (int)stream.Position;
        writer.Write(tmod.ModName);
        writer.Write(tmod.ModVersion);

        writer.Write(tmod.Files.Count);

        foreach (var kvp in tmod.Files)
        {
            var path = kvp.Key;
            var entry = kvp.Value;

            writer.Write(path);
            writer.Write(entry.UncompressedLength);
            writer.Write(entry.CompressedLength);
        }

        foreach (var entry in tmod.Files.Values)
        {
            writer.Write(entry.Data);
        }

        stream.Position = dataPos;
        var hash = sha1.ComputeHash(stream);

        stream.Position = hashPos;
        writer.Write(hash);

        stream.Seek(signature_length, SeekOrigin.Current);

        writer.Write((int)(stream.Length - dataPos));
    }

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
