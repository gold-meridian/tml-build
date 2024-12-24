using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Octokit;

using Tomat.TML.Build.Common.Util;

using FileMode = System.IO.FileMode;

namespace Tomat.TML.Build.Common;

/// <summary>
///     Manages and records known tModLoader versions.  Can resolve and cache as
///     requested.
/// </summary>
public static class ModLoaderVersionManager
{
    public readonly record struct GitHubRelease(
        ModLoaderVersion Version,
        string           DownloadUrl
    );

    public readonly record struct VersionCache(
        DateTime            LastUpdated,
        List<GitHubRelease> GitHubReleases,
        ModLoaderVersion    StableVersion,
        ModLoaderVersion    PreviewVersion
    );

    public static VersionCache Cache { get; }

    public static string? SteamPath { get; }

    public static string? DevPath { get; }

    static ModLoaderVersionManager()
    {
        Directory.CreateDirectory(Platform.GetAppDir());

        var versionCacheFile = Path.Combine(Platform.GetAppDir(), "cached_versions");
        if (File.Exists(versionCacheFile))
        {
            Cache = ReadCache(versionCacheFile);

            if (DateTime.Now - Cache.LastUpdated > TimeSpan.FromDays(1))
            {
                File.Delete(versionCacheFile);
                WriteCache(Cache = ResolveVersions(), versionCacheFile);
            }
        }
        else
        {
            WriteCache(Cache = ResolveVersions(), versionCacheFile);
        }

        SteamPath = Platform.GetSteamGamePath("tModLoader");
        DevPath   = Platform.GetSteamGamePath("tModLoaderDev");
    }

    private static VersionCache ResolveVersions()
    {
        var gh = new GitHubClient(
            new ProductHeaderValue(
                "Tomat.TML.Build.Common",
                typeof(ModLoaderVersionManager).Assembly.GetName().Version!.ToString()
            )
        );

        var releases = gh.Repository.Release.GetAll("tModLoader", "tModLoader").Result
                         .OrderByDescending(x => x.CreatedAt)
                         .ToArray();

        var stable  = releases.First(x => !x.Prerelease);
        var preview = releases.First(x => x.Prerelease);

        var gitHubReleases = new List<GitHubRelease>(releases.Length);
        foreach (var release in releases)
        {
            if (!TryParseVersion(release.TagName, out var version))
            {
                continue;
            }

            if (release.Assets.FirstOrDefault(x => x.Name == "tModLoader.zip") is not { } asset)
            {
                continue;
            }

            gitHubReleases.Add(new GitHubRelease(version, asset.BrowserDownloadUrl));
        }

        if (!TryParseVersion(stable.TagName, out var stableVersion))
        {
            stableVersion = new ModLoaderVersion(0, 0, 0, 0);
        }

        if (!TryParseVersion(preview.TagName, out var previewVersion))
        {
            previewVersion = new ModLoaderVersion(0, 0, 0, 0);
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

    private static bool TryParseVersion(string text, out ModLoaderVersion version)
    {
        text = text.TrimStart('v');
        if (Version.TryParse(text, out var sysVersion))
        {
            version = new ModLoaderVersion(sysVersion.Major, sysVersion.Minor, sysVersion.Build, sysVersion.Revision);
            return true;
        }

        version = default(ModLoaderVersion);
        return false;
    }
}