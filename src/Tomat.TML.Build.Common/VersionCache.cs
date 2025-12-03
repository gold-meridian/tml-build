using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Tomat.TML.Build.Common;

/// <summary>
///     Represents a cached version repository on disk.
/// </summary>
/// <param name="RootDirectory">
///     The root directory of this version cache.
/// </param>
/// <param name="LastUpdated">
///     The last time the release list was updated.
/// </param>
/// <param name="GitHubReleases">The known GitHub releases.</param>
/// <param name="StableVersion">The latest stable version.</param>
/// <param name="PreviewVersion">The latest preview version.</param>
/// <param name="SteamPath">The path to the Steam version, if present.</param>
/// <param name="DevPath">The path to the developer version, if present.</param>
public readonly record struct VersionCache(
    string RootDirectory,
    DateTime LastUpdated,
    List<VersionCache.Release> GitHubReleases,
    ModLoaderVersion StableVersion,
    ModLoaderVersion PreviewVersion,
    string? SteamPath,
    string? DevPath
)
{
    /// <summary>
    ///     A release object known by the cache.
    /// </summary>
    public readonly record struct Release(
        ModLoaderVersion Version,
        string DownloadUrl
    );

    /// <summary>
    ///     Whether a version is known by this cache.
    /// </summary>
    public bool IsVersionKnown(ModLoaderVersion version)
    {
        return GitHubReleases.Any(x => x.Version == version);
    }

    /// <summary>
    ///     Whether the version exists on disk.
    /// </summary>
    public bool IsVersionCached(ModLoaderVersion version)
    {
        return Directory.Exists(GetVersionDirectory(version));
    }

    /// <summary>
    ///     Gets the version directory.
    /// </summary>
    public string GetVersionDirectory(ModLoaderVersion version)
    {
        return Path.Combine(RootDirectory, version.ToString());
    }
    
    public async Task DownloadVersionAsync(ModLoaderVersion version)
    {
        if (IsVersionCached(version))
        {
            return;
        }

        if (!IsVersionKnown(version))
        {
            throw new ArgumentException($"Version '{version}' is not known.");
        }

        var release = GitHubReleases.First(x => x.Version == version);
        var tempFile = Path.GetTempFileName();
        {
            using var client = new HttpClient();
            using var stream = await client.GetStreamAsync(release.DownloadUrl);
            using var file = File.Create(tempFile);
            await stream.CopyToAsync(file);
        }

        ZipFile.ExtractToDirectory(tempFile, GetVersionDirectory(version));
        File.Delete(tempFile);
    }
}
