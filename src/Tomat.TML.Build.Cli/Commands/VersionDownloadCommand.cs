using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version download", Description = "Downloads the specified tModLoader version")]
public class VersionDownloadCommand : ICommand
{
    [CommandParameter(0, IsRequired = true)]
    public string Version { get; set; } = string.Empty;

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        var version = default(ModLoaderVersion);

        switch (Version.ToLower())
        {
            case "stable":
                version = ModLoaderVersion.Stable;
                break;

            case "preview":
                version = ModLoaderVersion.Preview;
                break;

            case "steam":
            case "dev":
                await console.Error.WriteLineAsync("Cannot download Steam or Dev versions.");
                Environment.ExitCode = 0; // it's fine tho
                return;

            default:
                if (!ModLoaderVersion.TryParse(Version, out version))
                {
                    await console.Error.WriteLineAsync("Invalid version format.");
                    Environment.ExitCode = 1;
                    return;
                }

                break;
        }

        if (!VersionManager.IsVersionKnown(version))
        {
            await console.Error.WriteLineAsync($"Version ({version}) is not known.");
            Environment.ExitCode = 1;
            return;
        }

        if (VersionManager.IsVersionCached(version))
        {
            await console.Error.WriteLineAsync($"Version ({version}) is already downloaded.");
            Environment.ExitCode = 0;
            return;
        }

        try
        {
            await console.Output.WriteLineAsync($"Downloading version ({version})...");
            await VersionManager.DownloadVersion(version);
            await console.Output.WriteLineAsync($"Downloaded version ({version}).");
            Environment.ExitCode = 0;
        }
        catch (Exception e)
        {
            await console.Error.WriteLineAsync("Failed to download version: " + e.Message);
            Environment.ExitCode = 1;
        }
    }
}
