using CliFx.Infrastructure;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.Cli;

internal readonly struct Logger(IConsole console) : ILogWrapper
{
    public void Info(string message)
    {
        console.Output.WriteLine(message);
    }

    public void Error(string message)
    {
        console.Error.WriteLine(message);
    }
}
