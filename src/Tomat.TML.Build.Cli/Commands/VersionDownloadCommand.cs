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

        await console.Output.WriteLineAsync("Downloading version...");
        await ModLoaderVersionManager.DownloadVersion(version);
        await console.Output.WriteLineAsync("Downloaded version.");
    }
}