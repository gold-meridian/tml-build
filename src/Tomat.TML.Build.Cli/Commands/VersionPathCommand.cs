using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version path", Description = "Returns the path to the specified tModLoader version")]
public class VersionPathCommand : ICommand
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
                if (VersionManager.SteamPath is null)
                {
                    await console.Error.WriteLineAsync("Steam path not found.");
                    Environment.ExitCode = 1;
                    return;
                }

                await console.Output.WriteLineAsync(VersionManager.SteamPath);
                Environment.ExitCode = 0;
                return;

            case "dev":
                if (VersionManager.DevPath is null)
                {
                    await console.Error.WriteLineAsync("Dev path not found.");
                    Environment.ExitCode = 1;
                    return;
                }

                await console.Output.WriteLineAsync(VersionManager.DevPath);
                Environment.ExitCode = 0;
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

        if (!VersionManager.IsVersionCached(version))
        {
            await console.Error.WriteLineAsync($"Version ({version}) is not downloaded.");
            Environment.ExitCode = 1;
            return;
        }

        await console.Output.WriteLineAsync(VersionManager.GetVersionDirectory(version));
    }
}
