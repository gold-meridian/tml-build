using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version download", Description = "Downloads the specified tModLoader version")]
public class VersionDownloadCommand : ICommand
{
    [CommandParameter(0, IsRequired = true)]
    public string Version { get; set; } = string.Empty;

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        var result = await VersionExecution.DownloadVersionAsync(Version, new Logger(console));
        Environment.ExitCode = result ? 0 : 1;
    }
}
