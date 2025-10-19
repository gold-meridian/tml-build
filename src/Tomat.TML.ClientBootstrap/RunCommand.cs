using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Tomat.TML.ClientBootstrap.Framework;

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

    [CommandOption("mod", Description = "The name of the requesting mod")]
    public string? ModName { get; init; }

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

        var bootstrapDir = Environment.CurrentDirectory;

        await console.Output.WriteLineAsync("Current working directory: " + bootstrapDir);
        await console.Output.WriteLineAsync("Target working directory: " + tmlDir);
        await console.Output.WriteLineAsync("Assembly to run: " + BinaryName);
        await console.Output.WriteLineAsync("Client/server mode: " + Mode);
        await console.Output.WriteLineAsync("Mod name: " + ModName);
        await console.Output.WriteLineAsync("Client features: " + string.Join(", ", enabledFeatures));
        await console.Output.WriteLineAsync($"Arguments to pass-through: {string.Join(' ', Program.PassThroughArguments)}");

        Environment.CurrentDirectory = tmlDir;
        await console.Output.WriteLineAsync("Set working directory: " + Environment.CurrentDirectory);

        var tmod = Assembly.LoadFile(Path.Combine(tmlDir, BinaryName));
        await console.Output.WriteLineAsync($"Loaded tModLoader assembly: {tmod}");

        var ctx = new LaunchContext(
            bootstrapDir,
            tmlDir,
            BinaryName,
            GetMode(Mode),
            ModName,
            enabledFeatures,
            Program.PassThroughArguments,
            new ArgumentRepository(Program.PrefixedArguments),
            tmod
        );

        LaunchWrapper.PatchAndRun(ctx);
    }

    private static LaunchMode GetMode(string modeValue)
    {
        return modeValue.ToLowerInvariant() switch
        {
            "client" => LaunchMode.Client,
            "server" => LaunchMode.Server,
            _ => LogError(modeValue),
        };

        static LaunchMode LogError(string mode)
        {
            Console.Error.WriteLine("Could not parse launch more into known value: " + mode);
            return LaunchMode.Client;
        }
    }
}
