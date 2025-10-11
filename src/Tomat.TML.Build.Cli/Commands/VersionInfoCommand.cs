using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version info", Description = "Displays tModLoader version cache info")]
public class VersionInfoCommand : ICommand
{
    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        var cache = ModLoaderVersionManager.Cache;
        var steamPath = ModLoaderVersionManager.SteamPath;
        var devPath = ModLoaderVersionManager.DevPath;

        var lastUpdated = cache.LastUpdated;
        await console.Output.WriteLineAsync("Last Updated: " + lastUpdated + " (" + (DateTime.Now - lastUpdated) + " ago)");
        await console.Output.WriteLineAsync();
        await console.Output.WriteLineAsync("Steam Path: " + (steamPath ?? "Not found"));
        await console.Output.WriteLineAsync("Dev Path: " + (devPath ?? "Not found"));
        await console.Output.WriteLineAsync();
        await console.Output.WriteLineAsync("Stable Version: " + cache.StableVersion);
        await console.Output.WriteLineAsync("Preview Version: " + cache.PreviewVersion);
        await console.Output.WriteLineAsync();
        await console.Output.WriteLineAsync("GitHub Releases:");
        foreach (var release in cache.GitHubReleases)
        {
            await console.Output.WriteLineAsync(release.Version + " - " + release.DownloadUrl);
        }
    }
}
