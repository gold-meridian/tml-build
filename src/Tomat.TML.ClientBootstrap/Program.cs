using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CliFx;

namespace Tomat.TML.ClientBootstrap;

internal static class Program
{
    /// <summary>
    ///     Arguments following after a '--' delimiter.
    /// </summary>
    public static string[] PassThroughArguments { get; private set; } = [];

    /// <summary>
    ///     Any arguments prefixed with a name delimited by ':', such as
    ///     <c>--plugin.name:flag</c> or
    ///     <c>--plugin.name:value &quot;foo&quot;</c>.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string?>> PrefixedArguments { get; private set; } = [];

    public static async Task<int> Main(string[] args)
    {
        args = SetPassThroughArguments(args);
        args = SetPrefixedArguments(args);

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

    private static string[] SetPrefixedArguments(string[] args)
    {
        var remainingArgs = new List<string>();
        var argDict = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--") || !arg.Contains(':'))
            {
                remainingArgs.Add(arg);
                continue;
            }

            var nameParts = arg[2..].Split(':', 2);
            if (nameParts.Length != 2)
            {
                remainingArgs.Add(arg);
                continue;
            }

            var name = nameParts[0];
            var key = nameParts[1];

            if (!argDict.TryGetValue(name, out var dict))
            {
                dict = argDict[name] = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            if (i + 1 >= args.Length)
            {
                dict[key] = null;
                continue;
            }

            var next = args[i + 1].Trim();
            if (next.StartsWith('-'))
            {
                dict[key] = null;
                continue;
            }

            i++;
            var value = next.Trim();

            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..];
            }

            dict[key] = value;
        }

        PrefixedArguments = argDict;
        return remainingArgs.ToArray();
    }
}
