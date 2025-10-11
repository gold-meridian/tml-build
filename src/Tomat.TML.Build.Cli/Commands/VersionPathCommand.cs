using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.Cli.Commands;

[Command("version path", Description = "Returns the path to the specified tModLoader version")]
public class VersionPathCommand : ICommand
{
    [CommandParameter(0, IsRequired = true)]
    public string Version { get; set; } = string.Empty;

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        var result = await VersionExecution.GetVersionPathAsync(Version, new Logger(console));
        if (result is null)
        {
            Environment.ExitCode = 1;
            return;
        }

        await console.Output.WriteLineAsync(result);
    }
}
