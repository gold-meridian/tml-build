using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;

namespace Tomat.TML.ClientBootstrap;

internal static class Program
{
    public static string[] PassThroughArguments { get; private set; } = [];

    public static async Task<int> Main(string[] args)
    {
        args = SetPassThroughArguments(args);

        return await new CliApplicationBuilder()
                    .SetTitle("tml bootstrap driver")
                    .SetDescription("Tomat.TML.Build-provided tModLoader launch wrapper to apply transient developer patches")
                    .AddCommandsFromThisAssembly()
                    .Build()
                    .RunAsync(args);
    }

    private static string[] SetPassThroughArguments(string[] args)
    {
        var sinkIndex = Array.IndexOf(args, "--", 0, args.Length);
        if (sinkIndex == -1)
        {
            return args;
        }

        PassThroughArguments = args[(sinkIndex + 1)..].ToArray();
        return args[..sinkIndex];
    }
}
