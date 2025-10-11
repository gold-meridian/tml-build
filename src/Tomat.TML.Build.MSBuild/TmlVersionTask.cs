using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.MSBuild;

public sealed class TmlVersionTask : Task
{
    [Required]
    public string TmlBuildPath { get; set; }

    [Required]
    public string Version { get; set; }

    [Required]
    public string TargetsPath { get; set; }

    public override bool Execute()
    {
        Log.LogMessage("Using tML version (unparsed): {0}", Version);

        // check if the process exited with a non-zero exit code
        if (!DownloadVersion(Version, Log))
        {
            Log.LogError($"Failed to download requested tML version in task, aborting: {Version}");
            return false;
        }

        if (GetPath(Version, Log) is not { } path)
        {
            Log.LogError("Failed to get the path of tML version {0}.", Version);
            return false;
        }

        Log.LogMessage("Got tML version path: {0}", path);

        var targetsCandidates = new[]
        {
            Path.Combine(path, "tMLMod.targets"),
            Path.Combine(path, "Build", "tMLMod.targets"),
        };

        var targets = targetsCandidates.FirstOrDefault(File.Exists);
        if (targets == null)
        {
            Log.LogError("Failed to find tML targets file in {0}.", path);
            return false;
        }

        // write a dummy targets file that imports the real targets file
        Directory.CreateDirectory(Path.GetDirectoryName(TargetsPath)!);
        Log.LogMessage(TargetsPath);
        File.WriteAllText(
            TargetsPath,
            $"""
             <Project>
                 <Import Project="{targets}"/>
             </Project>
             """
        );

        return true;
    }

    private static bool DownloadVersion(string tmlVersion, TaskLoggingHelper logger)
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
                    logger.LogError($"Invalid version format: {tmlVersion}");
                    return false;
                }

                break;
        }

        if (!ModLoaderVersionManager.IsVersionKnown(version))
        {
            logger.LogError($"Unknown tModLoader version: {version}");
            return false;
        }

        if (ModLoaderVersionManager.IsVersionCached(version))
        {
            logger.LogMessage($"Version is already cached: {version}");
            return true;
        }

        try
        {
            logger.LogMessage($"Downloading version: {version}...");
            ModLoaderVersionManager.DownloadVersion(version).GetAwaiter().GetResult();
            logger.LogMessage($"Downloaded version: {version}!");
            return true;
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to download version: {version}\n{e}");
            return false;
        }
    }

    private static string? GetPath(string tmlVersion, TaskLoggingHelper logger)
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
                if (ModLoaderVersionManager.SteamPath is null)
                {
                    logger.LogError("Failed to get path to Steam version; not found (is it installed?).");
                    return null;
                }

                return ModLoaderVersionManager.SteamPath;

            case "dev":
                if (ModLoaderVersionManager.DevPath is null)
                {
                    logger.LogError("Failed to get path to Dev version; not found (have you built it?).");
                    return null;
                }

                return ModLoaderVersionManager.DevPath;

            default:
                if (!ModLoaderVersion.TryParse(tmlVersion, out version))
                {
                    logger.LogError($"Invalid version format: {version}");
                    return null;
                }

                break;
        }

        if (!ModLoaderVersionManager.IsVersionKnown(version))
        {
            logger.LogError($"Unknown tModLoader version: {version}");
            return null;
        }

        if (!ModLoaderVersionManager.IsVersionCached(version))
        {
            logger.LogMessage($"Version is not installed (not cached): {version}");
            return null;
        }

        return ModLoaderVersionManager.GetVersionDirectory(version);
    }
}
