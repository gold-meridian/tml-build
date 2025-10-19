using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Tomat.Files.Tmod;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.MSBuild.Tasks;

// TODO: General things to make this procedure better.
//       1. Better errors for build property files:
//          - check TML.Mod.toml and build.txt case-invariant,
//          - check for *.Mod.toml pattern and given valid prefixes (TML)
//       2. See about supporting other forms (JSONC/JSON5, etc.),
//       3. Handle references properly.

public sealed class PackageModTask : BaseTask
{
#region References
    // Possible packages to include.
    [Required]
    public ITaskItem[] PackageReferences { get; set; } = [];

    // Possible projects to include.
    [Required]
    public ITaskItem[] ProjectReferences { get; set; } = [];

    // Possible assemblies to include.
    [Required]
    public ITaskItem[] ReferencePaths { get; set; } = [];

    // Referenced mods. TODO handle like packages
    [Required]
    public ITaskItem[] ModReferences { get; set; } = [];
#endregion

    // The directory of the project.
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    // The output path the .tmod file is written to.
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    // The assembly name of the project.
    [Required]
    public string AssemblyName { get; set; } = string.Empty;

    [Required]
    public string TmlVersion { get; set; } = string.Empty;

    public string TmodOutputPath { get; set; } = string.Empty;

    [Required]
    public string BuildFilePath { get; set; } = string.Empty;

    [Required]
    public string DescriptionFilePath { get; set; } = string.Empty;

    private static readonly string[] source_extensions = [".csproj", ".cs", ".sln"];

    protected override bool Run()
    {
        if (string.IsNullOrEmpty(TmodOutputPath))
        {
            TmodOutputPath = SavePathLocator.FindSavePath(TmlVersion, AssemblyName, Log);
        }

        Log.LogMessage($"Using path for .tmod file: {TmodOutputPath}");

        var modDllName = AssemblyName + ".dll";
        var modDllPath = Path.Combine(ProjectDirectory, OutputPath, modDllName);
        if (!File.Exists(modDllPath))
        {
            throw new FileNotFoundException($"Could not find assembly DLL to package: {modDllPath}");
        }

        Log.LogMessage($"Found assembly DLL to package: {modDllPath}");

        if (string.IsNullOrEmpty(BuildFilePath))
        {
            throw new FileNotFoundException("No build file specified (build.txt, TML.Mod.toml, etc.)");
        }

        if (!File.Exists(BuildFilePath))
        {
            throw new FileNotFoundException($"Build file not found: {BuildFilePath}");
        }

        Log.LogMessage($"Reading build file: {BuildFilePath}");
        var properties = GetModProperties(BuildFilePath);
        {
            properties.ModSource = ProjectDirectory;
        }

        if (string.IsNullOrEmpty(DescriptionFilePath))
        {
            throw new FileNotFoundException("No description file specified (description.txt)");
        }

        if (!File.Exists(DescriptionFilePath))
        {
            throw new FileNotFoundException($"Description file not found: {DescriptionFilePath}");
        }

        var description = File.ReadAllText(DescriptionFilePath);
        properties.Description = description;

        var tmlVersion = GetTmlVersion(TmlVersion);
        var tmodFile = new TmodFile(TmodOutputPath, AssemblyName, properties.Version, tmlVersion);

        tmodFile.AddFile(modDllName, File.ReadAllBytes(modDllPath));
        // AddAllReferences(tmodFile, properties);

        var modPdbPath = Path.ChangeExtension(modDllPath, ".pdb");
        var modPdbName = Path.ChangeExtension(modDllName, ".pdb");
        if (File.Exists(modPdbPath))
        {
            Log.LogMessage($"Found PDB to include: {modPdbPath}");
            tmodFile.AddFile(modPdbName, File.ReadAllBytes(modPdbPath));
            properties.EacPath = modPdbPath;
        }

        tmodFile.AddFile("Info", properties.ToBytes(tmlVersion.ToString()));

        Log.LogMessage("Adding resources to .tmod...");
        var resources = Directory.GetFiles(ProjectDirectory, "*", SearchOption.AllDirectories)
                                 .Where(x => !IgnoreResource(properties, x))
                                 .ToList();

        Parallel.ForEach(resources, x => AddResource(tmodFile, x));

        Log.LogMessage("Writing .tmod file...");
        try
        {
            tmodFile.Save();
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to write .tmod file, check that the mod isn't loaded if tmodLoader is open:\n{e}");
            return false;
        }

        var modsDir = Path.GetDirectoryName(TmodOutputPath);
        if (modsDir is null)
        {
            Log.LogWarning($"Couldn't traverse up output path to find Mods directory: {TmodOutputPath}");
            return true;
        }

        EnableMod(AssemblyName, modsDir);

        return true;
    }

    private void AddResource(TmodFile tmodFile, string resourcePath)
    {
        // TODO: Naive
        var relPath = resourcePath[(ProjectDirectory.Length + 1)..];

        Log.LogMessage($"Adding resource: {relPath}");

        using var fs = File.OpenRead(resourcePath);
        using var ms = new MemoryStream();

        if (!ContentConverters.Convert(ref relPath, fs, ms))
        {
            fs.CopyTo(ms);
        }

        tmodFile.AddFile(relPath, ms.ToArray());
    }

    private bool IgnoreResource(BuildProperties properties, string resourcePath)
    {
        // TODO: Naive
        var relPath = resourcePath[(ProjectDirectory.Length + 1)..];

        return properties.IgnoreFile(relPath)
            || relPath[0] == '.'
            || relPath.StartsWith("bin" + Path.DirectorySeparatorChar)
            || relPath.StartsWith("obj" + Path.DirectorySeparatorChar)
            || relPath == "build.txt"
            || relPath == "TML.Mod.toml"
            || !properties.IncludeSource && source_extensions.Contains(Path.GetExtension(resourcePath))
            || Path.GetFileName(resourcePath) == "Thumbs.db";
    }

    private void EnableMod(string modName, string modsDir)
    {
        var enabledPath = Path.Combine(modsDir, "enabled.json");
        if (!File.Exists(enabledPath))
        {
            Log.LogMessage($"enabled.json not found, mod will not be enabled: {enabledPath}");
            return;
        }

        try
        {
            var enabled = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(enabledPath)) ?? [];
            if (enabled.Contains(modName))
            {
                Log.LogMessage("Mod already enabled, skipping...");
                return;
            }

            enabled.Add(modName);
            File.WriteAllText(enabledPath, JsonConvert.SerializeObject(enabled, Formatting.Indented));
        }
        catch (Exception e)
        {
            Log.LogWarning($"Failed to enable mod '{modName}': {e}");
        }
    }

    private BuildProperties GetModProperties(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        if (fileName == "build.txt")
        {
            return BuildTxt();
        }

        if (fileName == "TML.Mod.toml")
        {
            return Toml();
        }

        throw new InvalidOperationException($"Don't know how to parse build file (is it supported?): {filePath}");

        BuildProperties BuildTxt()
        {
            var properties = BuildProperties.ReadBuildInfo(filePath, out var diagnostics, out var hasErrors);

            foreach (var diagnostic in diagnostics)
            {
                switch (diagnostic.MessageType.ToLowerInvariant())
                {
                    case "error":
                        Log.LogError(
                            subcategory: null,
                            errorCode: diagnostic.Code,
                            helpKeyword: null,
                            file: diagnostic.Path,
                            lineNumber: diagnostic.Location?.Line ?? 0,
                            columnNumber: diagnostic.Location?.Column ?? 0,
                            endLineNumber: 0,
                            endColumnNumber: 0,
                            message: diagnostic.Message
                        );
                        break;

                    case "warning":
                        Log.LogWarning(
                            subcategory: null,
                            warningCode: diagnostic.Code,
                            helpKeyword: null,
                            file: diagnostic.Path,
                            lineNumber: diagnostic.Location?.Line ?? 0,
                            columnNumber: diagnostic.Location?.Column ?? 0,
                            endLineNumber: 0,
                            endColumnNumber: 0,
                            message: diagnostic.Message
                        );
                        break;
                }
            }

            if (hasErrors)
            {
                throw new InvalidOperationException("build.txt file had fatal errors");
            }

            return properties;
        }

        BuildProperties Toml()
        {
            var text = File.ReadAllText(filePath);
            var toml = Tomlyn.Toml.ToModel(text);

            return null!;
        }
    }

    private Version GetTmlVersion(string tmlVersion)
    {
        return tmlVersion.ToLowerInvariant() switch
        {
            "stable" => ModLoaderVersion.Stable.ToSystemVersion(),
            "preview" => ModLoaderVersion.Preview.ToSystemVersion(),
            "dev" or "steam" => throw new NotImplementedException(),
            _ => Version.Parse(tmlVersion),
        };
    }
}
