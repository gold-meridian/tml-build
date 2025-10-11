using System;
using System.Threading.Tasks;

namespace Tomat.TML.Build.Common.Shared;

public static class VersionExecution
{
    public static async Task<bool> DownloadVersionAsync(string tmlVersion, ILogWrapper logger)
    {
        ModLoaderVersion version;

        switch (tmlVersion.ToLowerInvariant())
        {
            case "stable":
                version = ModLoaderVersion.Stable;
                break;

            case "preview":
                version = ModLoaderVersion.Preview;
                break;

            case "steam":
            case "dev":
                // Don't download anything, but these versions are valid.
                return true;

            default:
                if (!ModLoaderVersion.TryParse(tmlVersion, out version))
                {
                    await logger.Error($"Invalid version format: {tmlVersion}");
                    return false;
                }

                break;
        }

        if (!VersionManager.IsVersionKnown(version))
        {
            await logger.Error($"Unknown tModLoader version: {version}");
            return false;
        }

        if (VersionManager.IsVersionCached(version))
        {
            await logger.Info($"Version is already cached: {version}");
            return true;
        }

        try
        {
            await logger.Info($"Downloading version: {version}...");
            VersionManager.DownloadVersion(version).GetAwaiter().GetResult();
            await logger.Info($"Downloaded version: {version}!");
            return true;
        }
        catch (Exception e)
        {
            await logger.Error($"Failed to download version: {version}\n{e}");
            return false;
        }
    }

    public static bool DownloadVersion(string version, ILogWrapper logger)
    {
        return DownloadVersionAsync(version, logger).GetAwaiter().GetResult();
    }

    public static async Task<string?> GetVersionPathAsync(string tmlVersion, ILogWrapper logger)
    {
        ModLoaderVersion version;

        switch (tmlVersion.ToLowerInvariant())
        {
            case "stable":
                version = ModLoaderVersion.Stable;
                break;

            case "preview":
                version = ModLoaderVersion.Preview;
                break;

            case "steam":
                if (VersionManager.SteamPath is null)
                {
                    await logger.Error("Failed to get path to Steam version; not found (is it installed?).");
                    return null;
                }

                return VersionManager.SteamPath;

            case "dev":
                if (VersionManager.DevPath is null)
                {
                    await logger.Error("Failed to get path to Dev version; not found (have you built it?).");
                    return null;
                }

                return VersionManager.DevPath;

            default:
                if (!ModLoaderVersion.TryParse(tmlVersion, out version))
                {
                    await logger.Error($"Invalid version format: {version}");
                    return null;
                }

                break;
        }

        if (!VersionManager.IsVersionKnown(version))
        {
            await logger.Error($"Unknown tModLoader version: {version}");
            return null;
        }

        if (!VersionManager.IsVersionCached(version))
        {
            await logger.Info($"Version is not installed (not cached): {version}");
            return null;
        }

        return VersionManager.GetVersionDirectory(version);
    }

    public static string? GetVersionPath(string tmlVersion, ILogWrapper logger)
    {
        return GetVersionPathAsync(tmlVersion, logger).GetAwaiter().GetResult();
    }
}
