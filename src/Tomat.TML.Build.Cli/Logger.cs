using System.Threading.Tasks;
using CliFx.Infrastructure;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.Cli;

public readonly struct Logger(IConsole console) : ILogWrapper
{
    public async Task Info(string message)
    {
        await console.Output.WriteLineAsync(message);
    }
    
    public async Task Error(string message)
    {
        await console.Error.WriteLineAsync(message);
    }
}
