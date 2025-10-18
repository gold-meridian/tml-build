using System.Threading.Tasks;
using CliFx;

namespace Tomat.TML.ClientBootstrap;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await new CliApplicationBuilder()
                    .SetTitle("tml bootstrap driver")
                    .SetDescription("Tomat.TML.Build-provided tModLoader launch wrapper to apply developer patches")
                    .AddCommandsFromThisAssembly()
                    .Build()
                    .RunAsync(args);
    }
}
