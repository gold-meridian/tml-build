using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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

public sealed record BuildRevisionId(
    Version TmlVersion,
    Version StableVersion,
    string BranchName,
    BuildPurpose BuildPurpose,
    string CommitSha,
    DateTime BuildDate
);

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
            if (cache.SteamPath is null)
            {
                log.LogError("tML version explicitly selected as Steam, but Steam install path is not known");
            }
            else if (GetBuildRevisionFromModule(log, Path.Combine(cache.SteamPath, "tModLoader.dll"), out var revision))
            {
                log.LogMessage("tML version explicitly selected as steam, determined purpose: " + revision.BuildPurpose);
                return revision.BuildPurpose;
            }
        }

        log.LogError("Could not determine tML version, defaulting to stable");
        return BuildPurpose.Stable;
    }

    private static bool GetBuildRevisionFromModule(
        TaskLoggingHelper log,
        string tmlDllPath,
        [NotNullWhen(returnValue: true)] out BuildRevisionId? revision
    )
    {
        revision = null;

        var versionData = GetAssemblyInformationalVersionData(tmlDllPath);
        if (versionData is null)
        {
            log.LogError("Failed to get assembly informational version data from the tModLoader assembly");
            return false;
        }

        try
        {
            var parts = versionData[(versionData.IndexOf('+') + 1)..].Split('|');
            var tmlVersion = new Version(parts[0]);
            var stableVersion = new Version(parts[1]);
            var branchName = parts[2];
            var buildPurpose = (BuildPurpose)Enum.Parse(typeof(BuildPurpose), parts[3], ignoreCase: true);
            var commitSha = parts[4];
            var buildDate = DateTime.FromBinary(long.Parse(parts[5]));

            revision = new BuildRevisionId(
                tmlVersion,
                stableVersion,
                branchName,
                buildPurpose,
                commitSha,
                buildDate
            );
            return true;
        }
        catch (Exception e)
        {
            log.LogError($"Failed to parse assembly informational version data: {e}");
            return false;
        }
    }

    private static string? GetAssemblyInformationalVersionData(string dllPath)
    {
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);

        var mdReader = peReader.GetMetadataReader();

        foreach (var handle in mdReader.CustomAttributes)
        {
            var attr = mdReader.GetCustomAttribute(handle);

            var ctor = attr.Constructor;

            StringHandle nameHandle;
            if (ctor.Kind == HandleKind.MemberReference)
            {
                var memberRef = mdReader.GetMemberReference((MemberReferenceHandle)ctor);
                var container = memberRef.Parent;

                if (container.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var typeRef = mdReader.GetTypeReference((TypeReferenceHandle)container);
                nameHandle = typeRef.Name;
            }
            else
            {
                continue;
            }

            var attrTypeName = mdReader.GetString(nameHandle);
            if (attrTypeName != "AssemblyInformationalVersionAttribute")
            {
                continue;
            }

            // Decode the blob (fixed prolog + single string argument)
            var valueReader = mdReader.GetBlobReader(attr.Value);

            // Skip prolog (0x0001)
            valueReader.ReadUInt16();

            return valueReader.ReadSerializedString();
        }

        return null;
    }
}
