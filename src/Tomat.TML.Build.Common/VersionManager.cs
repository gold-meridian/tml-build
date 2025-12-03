using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Octokit;
using Tomat.TML.Build.Common.Utilities;
using FileMode = System.IO.FileMode;

namespace Tomat.TML.Build.Common;

/// <summary>
///     Manages and records known tModLoader versions.  Can resolve and cache as
///     requested.
/// </summary>
public static class VersionManager
{
    private const string cached_versions = "cached_versions";

    public static string DefaultCacheDir => Platform.GetAppDir();

    /// <summary>
    ///     Reads the cache at the given directory, creating it there if it
    ///     doesn't exist.
    /// </summary>
    public static VersionCache ReadOrCreateVersionCache(
        string versionCacheDir
    )
    {
        Directory.CreateDirectory(versionCacheDir);

        var versionCacheFile = Path.Combine(versionCacheDir, cached_versions);

        VersionCache cache;
        if (File.Exists(versionCacheFile))
        {
            cache = ReadCache(versionCacheDir);

            // Trigger a cache refresh every day, ideally.
            RefreshCache(ref cache, cooldown: TimeSpan.FromDays(1));
        }
        else
        {
            WriteCache(cache = ResolveVersions(versionCacheDir), versionCacheFile);
        }

        return cache;
    }

    /// <summary>
    ///     Refreshes the cache, updating known versions.
    /// </summary>
    /// <param name="cache">The cache to refresh.</param>
    /// <param name="cooldown">
    ///     The cooldown between cache updates.  If no value is given, an update
    ///     will be forced.
    /// </param>
    /// <returns>
    ///     Whether the cache needed updating based on the cooldown.
    /// </returns>
    public static bool RefreshCache(
        ref VersionCache cache,
        TimeSpan? cooldown
    )
    {
        if (cooldown.HasValue && DateTime.Now - cache.LastUpdated < cooldown.Value)
        {
            return false;
        }

        var versionCacheFile = Path.Combine(cache.RootDirectory, cached_versions);
        
        // Don't delete the cache file prematurely, just try to overwrite it and
        // see if that fails.
        /*
        if (File.Exists(versionCacheFile))
        {
            File.Delete(versionCacheFile);
        }
        */

        try
        {
            WriteCache(cache = ResolveVersions(cache.RootDirectory), versionCacheFile);
        }
        catch
        {
            // Not a big deal if we can't actually write the cache.  Either we
            // don't have permission or it's in use.  This may cause rate
            // limiting when requesting versions from GitHub, though.
        }

        return true;
    }

    private static VersionCache ResolveVersions(string versionCacheDir)
    {
        var gh = new GitHubClient(
            new ProductHeaderValue(
                "Tomat.TML.Build.Common",
                typeof(VersionManager).Assembly.GetName().Version!.ToString()
            )
        );

        var releases = gh.Repository.Release.GetAll("tModLoader", "tModLoader").Result
                         .OrderByDescending(x => x.PublishedAt)
                         .ToArray();

        var stable = releases.First(x => !x.Prerelease);
        var preview = releases.First(x => x.Prerelease);

        var gitHubReleases = new List<VersionCache.Release>(releases.Length);
        foreach (var release in releases)
        {
            if (!ModLoaderVersion.TryParse(release.TagName, out var version))
            {
                continue;
            }

            if (release.Assets.FirstOrDefault(x => x.Name == "tModLoader.zip") is not { } asset)
            {
                continue;
            }

            gitHubReleases.Add(new VersionCache.Release(version, asset.BrowserDownloadUrl));
        }

        if (!ModLoaderVersion.TryParse(stable.TagName, out var stableVersion))
        {
            stableVersion = ModLoaderVersion.Unknown;
        }

        if (!ModLoaderVersion.TryParse(preview.TagName, out var previewVersion))
        {
            previewVersion = ModLoaderVersion.Unknown;
        }

        return new VersionCache(
            versionCacheDir,
            DateTime.Now,
            gitHubReleases,
            stableVersion,
            previewVersion,
            Platform.GetSteamGamePath("tModLoader"),
            Platform.GetSteamGamePath("tModLoaderDev")
        );
    }

    private static void WriteCache(VersionCache cache, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // LastUpdated
        {
            bw.Write(cache.LastUpdated.ToBinary());
        }

        // GitHubReleases
        {
            bw.Write(cache.GitHubReleases.Count);
            foreach (var release in cache.GitHubReleases)
            {
                bw.Write(release.Version.Major);
                bw.Write(release.Version.Minor);
                bw.Write(release.Version.Patch);
                bw.Write(release.Version.Build);
                bw.Write(release.DownloadUrl);
            }
        }

        // StableVersion
        {
            bw.Write(cache.StableVersion.Major);
            bw.Write(cache.StableVersion.Minor);
            bw.Write(cache.StableVersion.Patch);
            bw.Write(cache.StableVersion.Build);
        }

        // PreviewVersion
        {
            bw.Write(cache.PreviewVersion.Major);
            bw.Write(cache.PreviewVersion.Minor);
            bw.Write(cache.PreviewVersion.Patch);
            bw.Write(cache.PreviewVersion.Build);
        }
    }

    private static VersionCache ReadCache(string versionCacheDir)
    {
        var cachedVersionsPath = Path.Combine(versionCacheDir, cached_versions);

        using var fs = new FileStream(cachedVersionsPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // LastUpdated
        var lastUpdated = DateTime.FromBinary(br.ReadInt64());

        // GitHubReleases
        var gitHubReleases = new List<VersionCache.Release>(br.ReadInt32());
        {
            for (var i = 0; i < gitHubReleases.Capacity; i++)
            {
                gitHubReleases.Add(
                    new VersionCache.Release(
                        new ModLoaderVersion(
                            br.ReadInt32(),
                            br.ReadInt32(),
                            br.ReadInt32(),
                            br.ReadInt32()
                        ),
                        br.ReadString()
                    )
                );
            }
        }

        // StableVersion
        var stableVersion = new ModLoaderVersion(
            br.ReadInt32(),
            br.ReadInt32(),
            br.ReadInt32(),
            br.ReadInt32()
        );

        // PreviewVersion
        var previewVersion = new ModLoaderVersion(
            br.ReadInt32(),
            br.ReadInt32(),
            br.ReadInt32(),
            br.ReadInt32()
        );

        return new VersionCache(
            versionCacheDir,
            lastUpdated,
            gitHubReleases,
            stableVersion,
            previewVersion,
            Platform.GetSteamGamePath("tModLoader"),
            Platform.GetSteamGamePath("tModLoaderDev")
        );
    }
}
