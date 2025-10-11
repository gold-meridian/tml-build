using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version cache", Description = "Invalidates and refreshes the cached tModLoader versions")]
public class VersionCacheCommand : ICommand
{
    [CommandOption("force", 'f', Description = "Forces the cache to be refreshed")]
    public bool Forced { get; set; }

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        if (ModLoaderVersionManager.RefreshCache(Forced))
        {
            await console.Output.WriteLineAsync("Cache refreshed successfully");
            Environment.ExitCode = 0;
        }
        else
        {
            await console.Error.WriteLineAsync("Cache already up to date");
            Environment.ExitCode = 1;
        }
    }
}
