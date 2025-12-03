using System;
using System.Threading.Tasks;

namespace Tomat.TML.Build.Common.Shared;

public static class VersionExecution
{
    public static async Task<bool> DownloadVersionAsync(VersionCache cache, string tmlVersion, ILogWrapper logger)
    {
        ModLoaderVersion version;

        switch (tmlVersion.ToLowerInvariant())
        {
            case "stable":
                version = cache.StableVersion;
                break;

            case "preview":
                version = cache.PreviewVersion;
                break;

            case "steam":
            case "dev":
                // Don't download anything, but these versions are valid.
                return true;

            default:
                if (!ModLoaderVersion.TryParse(tmlVersion, out version))
                {
                    logger.Error($"Invalid version format: {tmlVersion}");
                    return false;
                }

                break;
        }

        if (!cache.IsVersionKnown(version))
        {
            logger.Error($"Unknown tModLoader version: {version}");
            return false;
        }

        if (cache.IsVersionCached(version))
        {
            logger.Info($"Version is already cached: {version}");
            return true;
        }

        try
        {
            logger.Info($"Downloading version: {version}...");
            await cache.DownloadVersionAsync(version);
            logger.Info($"Downloaded version: {version}!");
            return true;
        }
        catch (Exception e)
        {
            logger.Error($"Failed to download version: {version}\n{e}");
            return false;
        }
    }

    public static bool DownloadVersion(VersionCache cache, string version, ILogWrapper logger)
    {
        return DownloadVersionAsync(cache, version, logger).GetAwaiter().GetResult();
    }

    public static string? GetVersionPath(VersionCache cache, string tmlVersion, ILogWrapper logger)
    {
        ModLoaderVersion version;

        switch (tmlVersion.ToLowerInvariant())
        {
            case "stable":
                version = cache.StableVersion;
                break;

            case "preview":
                version = cache.PreviewVersion;
                break;

            case "steam":
                if (cache.SteamPath is null)
                {
                    logger.Error("Failed to get path to Steam version; not found (is it installed?).");
                    return null;
                }

                return cache.SteamPath;

            case "dev":
                if (cache.DevPath is null)
                {
                    logger.Error("Failed to get path to Dev version; not found (have you built it?).");
                    return null;
                }

                return cache.DevPath;

            default:
                if (!ModLoaderVersion.TryParse(tmlVersion, out version))
                {
                    logger.Error($"Invalid version format: {version}");
                    return null;
                }

                break;
        }

        if (!cache.IsVersionKnown(version))
        {
            logger.Error($"Unknown tModLoader version: {version}");
            return null;
        }

        if (!cache.IsVersionCached(version))
        {
            logger.Info($"Version is not installed (not cached): {version}");
            return null;
        }

        return cache.GetVersionDirectory(version);
    }
}
