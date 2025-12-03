using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Utilities;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.MSBuild;

public enum BuildPurpose
{
    Stable,
    Preview,
    Dev,
}

public static class SavePathLocator
{
    private const string preview_dir = "tModLoader-preview";
    private const string stable_dir = "tModLoader";
    private const string dev_dir = "tModLoader-dev";

    public static string FindSavePath(
        VersionCache cache,
        string tmlVersion,
        string assemblyName,
        TaskLoggingHelper log
    )
    {
        var saveFolder = FindSaveFolder(cache, tmlVersion, log);

        return Path.Combine(saveFolder, "Mods", Path.ChangeExtension(assemblyName, ".tmod"));
    }

    private static string FindSaveFolder(VersionCache cache, string tmlVersion, TaskLoggingHelper log)
    {
        var branchDir = GetBuildPurpose(cache, tmlVersion, log) switch
        {
            BuildPurpose.Stable => stable_dir,
            BuildPurpose.Preview => preview_dir,
            BuildPurpose.Dev => dev_dir,
            _ => stable_dir,
        };

        var steamPath = cache.SteamPath;
        if (steamPath is null)
        {
            log.LogWarning("Could not locate Steam install path; not checking for savehere.txt");
        }
        else if (File.Exists(Path.Combine(steamPath, "savehere.txt")))
        {
            var path = Path.Combine(steamPath, branchDir);
            log.LogMessage($"Found savehere.txt; saving at path: {path}");
            return path;
        }

        var savePath = Path.Combine(GetStoragePath("Terraria"), branchDir);
        {
            log.LogMessage($"Saving at: {savePath}");
        }

        return savePath;
    }

    private static string GetStoragePath(string subDir)
    {
        return Path.Combine(GetStoragePath(), subDir);
    }

    private static string GetStoragePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var environmentVariable = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(environmentVariable))
            {
                return ".";
            }

            return environmentVariable + "/Library/Application Support";
        }

        var text = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(text))
        {
            return ".";
        }

        text += "/.local/share";

        return text;
    }

    private static BuildPurpose GetBuildPurpose(VersionCache cache, string tmlVersion, TaskLoggingHelper log)
    {
        tmlVersion = tmlVersion.ToLowerInvariant();

        log.LogMessage($"Determining build purpose based on tML version: {tmlVersion}");
        log.LogMessage($"Known special versions: stable({cache.StableVersion}), preview({cache.PreviewVersion}), dev, steam");

        if (ModLoaderVersion.TryParse(tmlVersion, out var version))
        {
            /*
            log.LogWarning($"Failed to parse tML version, defaulting to stable: {tmlVersion}");
            return BuildPurpose.Stable;
            */

            log.LogMessage("Parsed tML version as numeric version");

            if (version == cache.StableVersion)
            {
                log.LogMessage("Matched tML version with known stable");
                return BuildPurpose.Stable;
            }

            if (version == cache.PreviewVersion)
            {
                log.LogMessage("Matched tML version with known preview");
            }

            log.LogWarning("Numeric tML version does not correspond to known current stable or preview versions, assuming stable");
            return BuildPurpose.Stable;
        }

        if (tmlVersion == "stable")
        {
            log.LogMessage("tML version explicitly selected as stable");
            return BuildPurpose.Stable;
        }

        if (tmlVersion == "preview")
        {
            log.LogMessage("tML version explicitly selected as preview");
            return BuildPurpose.Preview;
        }

        if (tmlVersion == "dev")
        {
            log.LogMessage("tML version explicitly selected as dev");
            return BuildPurpose.Dev;
        }

        if (tmlVersion == "steam")
        {
            // TODO: We can fix this by manually inspecting the tModLoader
            //       assembly, but that's a lot of work.
            log.LogWarning("tML version explicitly selected as Steam, is it recommended to use stable, preview, or dev, as simply referencing the local Steam version does not disambiguate between stable and preview; assuming stable");
            return BuildPurpose.Stable;
        }

        log.LogError("Could not determine tML version, defaulting to stable");
        return BuildPurpose.Stable;
    }
}
