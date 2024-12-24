using System.Threading.Tasks;

using CliFx;

namespace Tomat.TML.Build.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await new CliApplicationBuilder()
                    .SetTitle("tml-build")
                    .SetDescription("tml mod authoring toolchain")
                    .AddCommandsFromThisAssembly()
                    .Build()
                    .RunAsync(args);
    }
}