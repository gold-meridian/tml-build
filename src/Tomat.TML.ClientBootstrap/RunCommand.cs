using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Features;

namespace Tomat.TML.ClientBootstrap;

[Command(Description = "Bootstraps the launch of tModLoader with optional features")]
public sealed class RunCommand : ICommand
{
    [CommandOption("working-directory", Description = "The tModLoader working directory")]
    public required string WorkingDirectory { get; init; }

    [CommandOption("binary", Description = "The tModLoader binary path name")]
    public required string BinaryName { get; init; }

    [CommandOption("mode", Description = "The mode, client or server")]
    public required string Mode { get; init; }

    [CommandOption("mod", Description = "The name of the mod to ensure is enabled")]
    public required string ModName { get; init; }

    [CommandOption("features", Description = "Semicolon-delimited list of feature patches")]
    public required string Features { get; init; }

    async ValueTask ICommand.ExecuteAsync(IConsole console)
    {
        var enabledFeatures = Features.Split(';').Select(x => x.ToLowerInvariant()).ToArray();

        // Remove the sentinel '.' used to avoid issues with trailing
        // backslashes.  Generally the path will end with a '\', so the
        // generated launchSettings includes an extra '.' to avoid escaping the
        // following quotation mark.  Shaves it off, which is unimportant
        // generally since '\.' resolves to '\', but may be important in the off
        // chance the property does not end with a backslash. 
        var tmlDir = WorkingDirectory;
        if (tmlDir.EndsWith('.'))
        {
            tmlDir = tmlDir[..^1];
        }

        await console.Output.WriteLineAsync("Current working directory: " + Environment.CurrentDirectory);
        await console.Output.WriteLineAsync("Target working directory: " + tmlDir);
        await console.Output.WriteLineAsync("Assembly to run: " + BinaryName);
        await console.Output.WriteLineAsync("Client/server mode: " + Mode);
        await console.Output.WriteLineAsync("Mod name: " + ModName);
        await console.Output.WriteLineAsync("Client features: " + string.Join(", ", enabledFeatures));

        Environment.CurrentDirectory = tmlDir;
        await console.Output.WriteLineAsync("Set working directory: " + Environment.CurrentDirectory);

        var tmod = Assembly.LoadFile(Path.Combine(tmlDir, BinaryName));
        await console.Output.WriteLineAsync($"tmod: {tmod}");

        PatchAndRun(BinaryName, Mode, ModName, enabledFeatures);
    }

    private static Task? hookTask;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PatchAndRun(string binaryName, string mode, string modName, string[] enabledFeatures)
    {
        var knownFeatures = new Dictionary<string, AssemblyFeature>()
        {
            { "ppeb.netcoredbg", new PpebNetCoreDbgFeature() },
            { "tomat.enablemod", new TomatEnableModFeature(modName) },
        };

        var features = knownFeatures.Where(x => enabledFeatures.Contains(x.Key))
                                    .Select(x => x.Value)
                                    .ToArray();

        hookTask = Task.Run(
            () => { }
        );

        // TODO: Support pass-through arguments.
        var args = new List<string> { "-console" };
        if (mode == "server")
        {
            args.Add("-server");
        }

        if (typeof(ModLoader).Assembly.EntryPoint is not { } entryPoint)
        {
            throw new InvalidOperationException("Cannot launch tML, no entrypoint found!");
        }

        entryPoint.Invoke(null, [args.ToArray()]);
    }
}
