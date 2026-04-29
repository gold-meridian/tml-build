using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    ///     The directory where extracted GitHub release zips are stored.
    /// </summary>
    public string ExtractDir => Path.Combine(RootDirectory, "versions");

    /// <summary>
    ///     The directory used as a local NuGet feed for GitHub releases.
    /// </summary>
    public string FeedDir => Path.Combine(RootDirectory, "feed");

    /// <summary>
    ///     Whether a version is known by this cache.
    /// </summary>
    public bool IsVersionKnown(ModLoaderVersion version)
    {
        return GitHubReleases.Any(x => x.Version == version);
    }

    /// <summary>
    ///     Whether the version has been extracted to disk.
    /// </summary>
    public bool IsVersionExtracted(ModLoaderVersion version)
    {
        return Directory.Exists(GetVersionExtractDir(version));
    }

    /// <summary>
    ///     Whether a nupkg for this version already exists in the local feed.
    /// </summary>
    public bool IsVersionPackaged(ModLoaderVersion version)
    {
        return File.Exists(GetNupkgPath(version));
    }

    /// <summary>
    ///     Gets the extraction directory for a specific GitHub release version.
    /// </summary>
    public string GetVersionExtractDir(ModLoaderVersion version)
    {
        return Path.Combine(ExtractDir, version.ToString());
    }

    /// <summary>
    ///     Gets the path to the nupkg in the local feed for a given version.
    /// </summary>
    public string GetNupkgPath(ModLoaderVersion version)
    {
        return Path.Combine(FeedDir, $"tModLoader.{version}.nupkg");
    }

    /// <summary>
    ///     Resolves the path to tMLMod.targets for a GitHub-sourced version,
    ///     searching both the extraction root and a Build/ subdirectory.
    ///     <br />
    ///     Returns null if the version hasn't been extracted yet.
    /// </summary>
    public string? GetTargetsPath(ModLoaderVersion version)
    {
        var root = GetVersionExtractDir(version);
        var candidates = new[]
        {
            Path.Combine(root, "tMLMod.targets"),
            Path.Combine(root, "Build", "tMLMod.targets"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    ///     Resolves the path to tMLMod.targets for a local (Steam/dev) version.
    ///     <br />
    ///     Returns null if the path is not set or the file doesn't exist.
    /// </summary>
    public string? GetLocalTargetsPath(string? localPath)
    {
        if (localPath is null)
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(localPath, "tMLMod.targets"),
            Path.Combine(localPath, "Build", "tMLMod.targets"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    ///     Downloads and extracts a GitHub release version if not already on
    ///     disk.  Does nothing if the version is already extracted.
    /// </summary>
    public async Task EnsureVersionExtractedAsync(ModLoaderVersion version)
    {
        if (IsVersionExtracted(version))
        {
            return;
        }

        if (!IsVersionKnown(version))
        {
            throw new ArgumentException($"Version '{version}' is not known.");
        }

        var release = GitHubReleases.First(x => x.Version == version);
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var client = new HttpClient())
            {
                using var stream = await client.GetStreamAsync(release.DownloadUrl);
                using var file = File.Create(tempFile);
                await stream.CopyToAsync(file);
            }

            var extractDir = GetVersionExtractDir(version);
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(tempFile, extractDir);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Creates a nupkg in the local feed for the given version, extracting
    ///     it first if necessary.  The nupkg wraps tMLMod.targets and all DLLs
    ///     from the extraction directory so NuGet can import them
    ///     automatically.
    /// </summary>
    public async Task EnsureVersionPackagedAsync(ModLoaderVersion version)
    {
        if (IsVersionPackaged(version))
        {
            return;
        }

        await EnsureVersionExtractedAsync(version);

        var targetsPath = GetTargetsPath(version)
                       ?? throw new InvalidOperationException($"Could not find tMLMod.targets for version '{version}' after extraction.");

        var extractDir = GetVersionExtractDir(version);
        var nupkgPath = GetNupkgPath(version);

        Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath)!);

        await Task.Run(() => BuildNupkg(version, extractDir, targetsPath, nupkgPath));
    }

    // Assembly files and satellite files like debug info and summaries.
    private static readonly string[] assembly_file_extensions = [".dll", ".pdb", ".xml"];

    private static void BuildNupkg(
        ModLoaderVersion version,
        string extractDir,
        string targetsPath,
        string nupkgPath
    )
    {
        var tmlRoot = Path.GetDirectoryName(targetsPath)!;
        var libraryDir = Path.Combine(extractDir, "Libraries");

        // Write to a temp file first so a crashed/partial write doesn't leave a
        // corrupt nupkg in the feed that NuGet would happily cache as valid.
        var tempNupkg = nupkgPath + ".tmp";

        try
        {
            using (var zip = ZipFile.Open(tempNupkg, ZipArchiveMode.Create))
            {
                // .nuspec
                var nuspecEntry = zip.CreateEntry("tModLoader.nuspec", CompressionLevel.Optimal);
                using (var sw = new StreamWriter(nuspecEntry.Open()))
                {
                    sw.Write(BuildNuspec(version));
                }

                // build/tModLoader.targets
                // NuGet auto-imports build/<packageId>.targets for every
                // PackageReference, so we rename the file to match the package
                // ID.
                var tmlTargetsEntry = zip.CreateEntry("build/tModLoader.targets", CompressionLevel.Optimal);
                using (var sw = new StreamWriter(tmlTargetsEntry.Open()))
                {
                    sw.Write(ModifyTmlTargets(targetsPath, extractDir));
                }

                // Carry a .props if one exists alongside the targets.
                var propsPath = Path.ChangeExtension(targetsPath, ".props");
                if (File.Exists(propsPath))
                {
                    zip.CreateEntryFromFile(
                        propsPath,
                        "build/tModLoader.props",
                        CompressionLevel.Optimal
                    );
                }

                // lib/net8.0/tModLoader.dll
                // The main assembly lives next to tMLMod.targets.
                foreach (var ext in assembly_file_extensions)
                {
                    var tmlFile = Path.Combine(tmlRoot, "tModLoader" + ext);
                    if (File.Exists(tmlFile))
                    {
                        zip.CreateEntryFromFile(tmlFile, "lib/net8.0/tModLoader" + ext, CompressionLevel.Optimal);
                    }
                }

                // lib/net8.0/ -> Libraries/**/*.dll
                // Mirror the ItemGroup from tMLMod.targets exactly:
                //   Include  Libraries/**/*.dll
                //   Exclude  Libraries/Native/**
                //   Exclude  Libraries/**/runtime*/**
                //   Exclude  Libraries/**/*.resources.dll
                //   Exclude  Libraries/tModCodeAssist/**   (goes to analyzers/)
                if (Directory.Exists(libraryDir))
                {
                    foreach (var ext in assembly_file_extensions)
                    {
                        foreach (var file in Directory.EnumerateFiles(libraryDir, "*" + ext, SearchOption.AllDirectories))
                        {
                            if (IsExcludedLibraryDll(libraryDir, file))
                            {
                                continue;
                            }

                            // Flatten into lib/net8.0/; reference assembly
                            // consumers don't need the Libraries subfolder
                            // hierarchy.
                            zip.CreateEntryFromFile(
                                file,
                                $"lib/net8.0/{Path.GetFileName(file)}",
                                CompressionLevel.Optimal
                            );
                        }
                    }
                }

                // analyzers/dotnet/cs/ (tModCodeAssist)
                // NuGet expects analyzer assemblies under analyzers/dotnet/cs/.
                // tMLMod.targets hard-codes the 1.0.0 subdirectory,
                // so match that.
                var codeAssistDir = Path.Combine(libraryDir, "tModCodeAssist", "1.0.0");
                if (Directory.Exists(codeAssistDir))
                {
                    foreach (var dll in Directory.EnumerateFiles(codeAssistDir, "*.dll", SearchOption.TopDirectoryOnly))
                    {
                        zip.CreateEntryFromFile(
                            dll,
                            $"analyzers/dotnet/cs/{Path.GetFileName(dll)}",
                            CompressionLevel.Optimal
                        );
                    }
                }

                // [Content_Types].xml
                var ctEntry = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
                using (var sw = new StreamWriter(ctEntry.Open()))
                {
                    sw.Write(BuildContentTypes());
                }
            }

            // Atomic replace.
            if (File.Exists(nupkgPath))
            {
                File.Delete(nupkgPath);
            }

            File.Move(tempNupkg, nupkgPath);
        }
        catch
        {
            // Clean up the partial temp file so future runs try again cleanly.
            if (File.Exists(tempNupkg))
            {
                File.Delete(tempNupkg);
            }

            throw;
        }
    }

    private static string ModifyTmlTargets(string targetsPath, string extractDir)
    {
        var lines = File.ReadAllLines(targetsPath).ToList();

        // Replace $(MSBuildThisFileDirectory) with the extract dir.
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i] = lines[i].Replace("$(MSBuildThisFileDirectory)", extractDir + '\\');
        }

        // Remove References and Analyzers.
        lines.RemoveAll(x => x.Contains("<Reference "));
        lines.RemoveAll(x => x.Contains("<Analyzer "));

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsExcludedLibraryDll(string libraryDir, string dllPath)
    {
        var basePrefix = libraryDir.Replace('\\', '/').TrimEnd('/') + '/';
        var fullNorm = dllPath.Replace('\\', '/');
        var rel = fullNorm.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)
            ? fullNorm[basePrefix.Length..]
            : fullNorm;

        // Libraries/Native/**
        if (rel.StartsWith("Native/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Any path segment that starts with "runtime"
        // Libraries/**/runtime*/**
        foreach (var segment in rel.Split('/'))
        {
            if (segment.StartsWith("runtime", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Libraries/**/*.resources.dll
        if (rel.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Handled separately as analyzers:
        // Libraries/tModCodeAssist/**
        if (rel.StartsWith("tModCodeAssist/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string BuildNuspec(ModLoaderVersion version)
    {
        return
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>tModLoader</id>
                 <version>{version}</version>
                 <authors>tModLoader Team</authors>
                 <description>tModLoader {version} reference assemblies and build targets, packaged by Tomat.TML.Build.</description>
                 <requireLicenseAcceptance>false</requireLicenseAcceptance>
                 <developmentDependency>true</developmentDependency>
               </metadata>
             </package>
             """;
    }

    private static string BuildContentTypes()
    {
        return
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="nuspec" ContentType="application/octet"/>
              <Default Extension="dll" ContentType="application/octet"/>
              <Default Extension="targets" ContentType="application/octet"/>
              <Default Extension="props" ContentType="application/octet"/>
              <Default Extension="xml" ContentType="application/octet"/>
            </Types>
            """;
    }
}
