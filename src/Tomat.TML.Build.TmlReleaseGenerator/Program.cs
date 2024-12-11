using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Octokit;

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