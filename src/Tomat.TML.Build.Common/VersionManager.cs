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
    public readonly record struct GitHubRelease(
        ModLoaderVersion Version,
        string DownloadUrl
    );

    public readonly record struct VersionCache(
        DateTime LastUpdated,
        List<GitHubRelease> GitHubReleases,
        ModLoaderVersion StableVersion,
        ModLoaderVersion PreviewVersion
    );

    static VersionManager()
    {
        Directory.CreateDirectory(Platform.GetAppDir());

        var versionCacheFile = Path.Combine(Platform.GetAppDir(), "cached_versions");
        if (File.Exists(versionCacheFile))
        {
            Cache = ReadCache(versionCacheFile);

            // Trigger a cache refresh every day, ideally.
            RefreshCache(forced: false, cooldown: TimeSpan.FromDays(1));
        }
        else
        {
            WriteCache(Cache = ResolveVersions(), versionCacheFile);
        }

        SteamPath = Platform.GetSteamGamePath("tModLoader");
        DevPath = Platform.GetSteamGamePath("tModLoaderDev");
    }

    public static VersionCache Cache { get; private set; }

    public static string? SteamPath { get; }

    public static string? DevPath { get; }

    public static bool RefreshCache(bool forced, TimeSpan cooldown)
    {
        if (!forced && DateTime.Now - Cache.LastUpdated < cooldown)
        {
            return false;
        }

        var versionCacheFile = Path.Combine(Platform.GetAppDir(), "cached_versions");
        if (File.Exists(versionCacheFile))
        {
            File.Delete(versionCacheFile);
        }

        try
        {
            WriteCache(Cache = ResolveVersions(), versionCacheFile);
        }
        catch
        {
            // Not a big deal if we can't actually write the cache.  Either we
            // don't have permission or it's in use.  This may cause rate
            // limiting when requesting versions from GitHub, though.
        }
        return true;
    }

    public static bool IsVersionKnown(ModLoaderVersion version)
    {
        return Cache.GitHubReleases.Any(x => x.Version == version);
    }

    public static bool IsVersionCached(ModLoaderVersion version)
    {
        return Directory.Exists(GetVersionDirectory(version));
    }

    public static async Task DownloadVersionAsync(ModLoaderVersion version)
    {
        if (IsVersionCached(version))
        {
            return;
        }

        if (!IsVersionKnown(version))
        {
            throw new ArgumentException($"Version '{version}' is not known.");
        }

        var release = Cache.GitHubReleases.First(x => x.Version == version);
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

    public static string GetVersionDirectory(ModLoaderVersion version)
    {
        return Path.Combine(Platform.GetAppDir(), version.ToString());
    }

    private static VersionCache ResolveVersions()
    {
        var gh = new GitHubClient(
            new ProductHeaderValue(
                "Tomat.TML.Build.Common",
                typeof(VersionManager).Assembly.GetName().Version!.ToString()
            )
        );

        var releases = gh.Repository.Release.GetAll("tModLoader", "tModLoader").Result
                         .OrderByDescending(x => x.CreatedAt)
                         .ToArray();

        var stable = releases.First(x => !x.Prerelease);
        var preview = releases.First(x => x.Prerelease);

        var gitHubReleases = new List<GitHubRelease>(releases.Length);
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

            gitHubReleases.Add(new GitHubRelease(version, asset.BrowserDownloadUrl));
        }

        if (!ModLoaderVersion.TryParse(stable.TagName, out var stableVersion))
        {
            stableVersion = ModLoaderVersion.UNKNOWN;
        }

        if (!ModLoaderVersion.TryParse(preview.TagName, out var previewVersion))
        {
            previewVersion = ModLoaderVersion.UNKNOWN;
        }

        return new VersionCache(
            DateTime.Now,
            gitHubReleases,
            stableVersion,
            previewVersion
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

    private static VersionCache ReadCache(string file)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // LastUpdated
        var lastUpdated = DateTime.FromBinary(br.ReadInt64());

        // GitHubReleases
        var gitHubReleases = new List<GitHubRelease>(br.ReadInt32());
        {
            for (var i = 0; i < gitHubReleases.Capacity; i++)
            {
                gitHubReleases.Add(
                    new GitHubRelease(
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
            lastUpdated,
            gitHubReleases,
            stableVersion,
            previewVersion
        );
    }
}
