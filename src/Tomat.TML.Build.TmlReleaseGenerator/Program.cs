using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Octokit;

using Tomat.TML.Build.TmlReleaseGenerator;

// Assumes repository root as working directory.

if (args.Length != 1)
{
    Console.WriteLine("Usage: TmlReleaseGenerator <version>");
}

var gh = new GitHubClient(
    new ProductHeaderValue(
        "Tomat.TML.Build.TmlReleaseGenerator",
        typeof(Program).Assembly.GetName().Version!.ToString()
    )
);

// Sanity checking test code.
// foreach (var version in (string[])["stable", "preview", "2024.10.2.2", "2024.09.3.0"])
// {
//     var parsedVersion = await ParseVersion(version, gh.Repository);
//     Console.WriteLine($"{version}: {parsedVersion}");
// }

var versionText = "";

if (Environment.GetEnvironmentVariable("TRG_MANUAL_VERSION_OVERRIDE") is not { } versionOverride)
{
    var (version, release) = await ParseVersion(args[0], gh.Repository);
    Debug.Assert(args[0] != "preview" || release.Prerelease);
    Console.WriteLine($"'{args[0]}' is version {version}.");

    // Download the tModLoader.zip release file.
    var releaseAsset = release.Assets.First(x => x.Name == "tModLoader.zip");
    var asset        = await gh.Repository.Release.GetAsset("tModLoader", "tModLoader", releaseAsset.Id);

    var client = new HttpClient();

    // Save the file to the output directory.
    {
        if (File.Exists("tModLoader.zip"))
        {
            File.Delete("tModLoader.zip");
        }

        await using var stream = await client.GetStreamAsync(asset.BrowserDownloadUrl);
        await using var file   = File.Create("tModLoader.zip");
        await stream.CopyToAsync(file);
    }

    versionText = version.ToString();
}
else
{
    versionText = versionOverride;
}

// Extract the contents of the zip file.
{
    if (Directory.Exists("tModLoader"))
    {
        Directory.Delete("tModLoader", recursive: true);
    }

    ZipFile.ExtractToDirectory("tModLoader.zip", "tModLoader");
}

// Resolve assemblies to package.
var assemblies = new List<PackagedAssembly>();
{
    // Special case for tModLoader.
    assemblies.Add(new PackagedAssembly("tModLoader", "tModLoader/tModLoader.dll", "tModLoader/tModLoader.xml", "tModLoader/tModLoader.pdb"));

    // Search tModLoader/Libraries.
    var libDir = Path.Combine("tModLoader", "Libraries");
    foreach (var assemblyDir in Directory.EnumerateDirectories(libDir, "*", SearchOption.TopDirectoryOnly))
    {
        var name = Path.GetFileName(assemblyDir);

        var dlls = Directory.EnumerateFiles(assemblyDir, "*.dll", SearchOption.AllDirectories).ToList();

        string  dllPath;
        string? xmlPath;
        string? pdbPath;

        if (dlls.Count == 0)
        {
            // If there are no DLLs, skip.
            continue;
        }

        if (dlls.Count == 1)
        {
            // If there is a single DLL, assume it's the assembly.
            dllPath = dlls[0];

            // Check for XML and PDB files (assume they're present... maybe).
            xmlPath = Directory.EnumerateFiles(assemblyDir, "*.xml", SearchOption.AllDirectories).FirstOrDefault();
            pdbPath = Directory.EnumerateFiles(assemblyDir, "*.pdb", SearchOption.AllDirectories).FirstOrDefault();
        }
        else
        {
            // In the event there's multiple DLLs, assume the DLL highest in the
            // directory structure is the assembly.
            var dll = dlls.OrderBy(d => d.Count(c => c == Path.DirectorySeparatorChar)).Last();
            dllPath = dll;

            // Check for XML and PDB files (assume they're present... maybe).
            xmlPath = Directory.EnumerateFiles(Path.GetDirectoryName(dll)!, "*.xml", SearchOption.AllDirectories).FirstOrDefault();
            pdbPath = Directory.EnumerateFiles(Path.GetDirectoryName(dll)!, "*.pdb", SearchOption.AllDirectories).FirstOrDefault();
        }

        assemblies.Add(new PackagedAssembly(name, dllPath, xmlPath, pdbPath));
    }
}

// Copy assemblies to build dir.
{
    const string dest_dir = "src/Tomat.Terraria.ModLoader/build/lib/net8.0/";
    if (Directory.Exists(dest_dir))
    {
        Directory.Delete(dest_dir, recursive: true);
    }
    Directory.CreateDirectory(dest_dir);

    foreach (var (name, dllToCopy, xmlPath, pdbPath) in assemblies)
    {
        var dllDest = Path.Combine(dest_dir, name + ".dll");
        var xmlDest = xmlPath is not null ? Path.Combine(dest_dir, name + ".xml") : null;
        var pdbDest = pdbPath is not null ? Path.Combine(dest_dir, name + ".pdb") : null;

        if (File.Exists(dllDest))
        {
            File.Delete(dllDest);
        }

        File.Copy(dllToCopy, dllDest);

        if (xmlPath is not null && xmlDest is not null)
        {
            if (File.Exists(xmlDest))
            {
                File.Delete(xmlDest);
            }

            File.Copy(xmlPath, xmlDest);
        }

        if (pdbPath is not null && pdbDest is not null)
        {
            if (File.Exists(pdbDest))
            {
                File.Delete(pdbDest);
            }

            File.Copy(pdbPath, pdbDest);
        }
    }
}

await CreateNuspec(versionText);

// run dotnet pack "src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.csproj" -c Release
var process = Process.Start(
    new ProcessStartInfo
    {
        FileName               = "dotnet",
        Arguments              = "pack src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.csproj -c Release -o ",
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
    }
);
await process!.WaitForExitAsync();

// Clean up remaining files.
File.Delete("tModLoader.zip");
Directory.Delete("tModLoader", recursive: true);
File.Delete("src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.nuspec");

return;

static async Task<(Version, Release)> ParseVersion(string version, IRepositoriesClient repo)
{
    version = version.ToLower();

    if (version == "stable")
    {
        // GetLatest filters to non-prerelease versions.
        var latest = await repo.Release.GetLatest("tModLoader", "tModLoader");
        return (Version.Parse(latest.TagName[1..]), latest);
    }

    var releases = await repo.Release.GetAll("tModLoader", "tModLoader");

    if (version == "preview")
    {
        // We have to filter for prerelease versions.
        var preview = releases
                     .Where(r => r.Prerelease)
                     .OrderByDescending(r => r.CreatedAt)
                     .First();
        return (Version.Parse(preview.TagName[1..]), preview);
    }

    // Find the exact version number.
    var release = releases.FirstOrDefault(r => r.TagName == $"v{version}");
    return release is null ? throw new ArgumentException("Version not found.") : (Version.Parse(version), release);
}

static async Task CreateNuspec(string version)
{
    if (File.Exists("src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.nuspec"))
    {
        File.Delete("src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.nuspec");
    }

    var text = """
               <package>
                   <metadata>
                       <id>Tomat.Terraria.ModLoader</id>
                       <version>$VERSION</version>
                       <authors>tml team, tomat</authors>
                       <license type="expression">MIT</license>
                       <projectUrl>https://github.com/tomat/tml-build</projectUrl>
                       <description>Packaged tModLoader binaries</description>
                       <tags>terraria tmodloader</tags>
                       <repository type="git" url="https://github.com/steviegt6/tml-build"/>
                       <dependencies>
                           <group targetFramework="net8.0"/>
                       </dependencies>
                   </metadata>
               </package>
               """.Replace("$VERSION", version);

    await File.WriteAllTextAsync("src/Tomat.Terraria.ModLoader/Tomat.Terraria.ModLoader.nuspec", text);
}