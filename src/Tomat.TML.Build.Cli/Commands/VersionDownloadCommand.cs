using System;
using System.Threading.Tasks;

using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version download")]
public class VersionDownloadCommand : ICommand
{
    [CommandParameter(0, IsRequired = true)]
    public string Version { get; set; } = string.Empty;

    [CommandOption("check", 'c', Description = "Simply checks if the version is downloaded")]
    public bool CheckDownloaded { get; set; }

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        if (!ModLoaderVersion.TryParse(Version, out var version))
        {
            await console.Error.WriteLineAsync("Invalid version format.");
            Environment.ExitCode = 1;
            return;
        }

        if (!ModLoaderVersionManager.IsVersionKnown(version))
        {
            await console.Error.WriteLineAsync("Version is not known.");
            Environment.ExitCode = 1;
            return;
        }

        if (CheckDownloaded)
        {
            if (ModLoaderVersionManager.IsVersionCached(version))
            {
                await console.Output.WriteLineAsync("Version is downloaded.");
                Environment.ExitCode = 0;
            }
            else
            {
                await console.Output.WriteLineAsync("Version is not downloaded.");
                Environment.ExitCode = 1;
            }
            return;
        }

        await console.Output.WriteLineAsync("Downloading version...");
        await ModLoaderVersionManager.DownloadVersion(version);
        await console.Output.WriteLineAsync("Downloaded version.");
    }
}