using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json.Linq;
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

        var tmod = InstallAssemblyResolver(tmlDir, BinaryName);
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
            tmod,
            new HookManager()
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

    private const string deps_ext = ".deps.json";
    private const string rc_ext = ".runtimeconfig.json";
    private const string rc_dev_ext = ".runtimeconfig.dev.json";

    private static readonly Dictionary<string, Assembly> assemblies = [];

    private static Assembly InstallAssemblyResolver(string baseDir, string binaryName)
    {
        Console.WriteLine("Setting up assembly resolver...");

        var assemblyName = Path.GetFileNameWithoutExtension(binaryName);
        Console.WriteLine("Will attempt to resolve assembly name: " + assemblyName);

        var binaryPath = Path.Combine(baseDir, binaryName);
        var depsPath = Path.Combine(baseDir, assemblyName + deps_ext);
        var rcPath = Path.Combine(baseDir, assemblyName + rc_ext);
        var rcDevPath = Path.Combine(baseDir, assemblyName + rc_dev_ext);
        Console.WriteLine("Assembly resolve-related paths:");
        Console.WriteLine($"    {binaryPath}");
        Console.WriteLine($"    {depsPath}");
        Console.WriteLine($"    {rcPath}");
        Console.WriteLine($"    {rcDevPath}");

        Console.WriteLine("Attempting to build dependencies list...");
        if (!File.Exists(depsPath))
        {
            throw new FileNotFoundException($"Cannot build dependencies list, no .deps file: {depsPath}");
        }

        using var depsStream = File.OpenRead(depsPath);
        var depReader = new DependencyContextJsonReader();
        var depContext = depReader.Read(depsStream);

        /*
        var depsJson = JObject.Parse(File.ReadAllText(depsPath));
        var libraries = (JObject?)depsJson["libraries"] ?? new JObject();

        var dependencies = new List<(string name, string version, string basePath)>();

        foreach (var (libName, libToken) in libraries)
        {
            var splitName = libName.Split('/', 2);

            // It's possible for path to be missing; just assume default dir.
            dependencies.Add((splitName[0], splitName[1], libToken?["path"]?.Value<string>() ?? string.Empty));
        }

        Console.WriteLine("Got dependencies:");
        foreach (var dep in dependencies)
        {
            Console.WriteLine($"    {dep.name}/{dep.version} (basePath={dep.basePath})");
        }*/

        var probePaths = new List<string> { baseDir };
        Console.WriteLine("Finding probing paths...");
        var runtimeConfigs = new[] { rcPath, rcDevPath };
        foreach (var rc in runtimeConfigs)
        {
            Console.WriteLine($"    Reading runtime config: {rc}");

            if (!File.Exists(rc))
            {
                Console.WriteLine("        Not found, skipping...");
                continue;
            }

            var rcJson = JObject.Parse(File.ReadAllText(rc));
            var paths = (JArray?)rcJson["runtimeOptions"]?["additionalProbingPaths"];
            var additionalProbingPaths = paths?.Select(p => p.ToString()).ToList() ?? [];

            if (additionalProbingPaths.Count == 0)
            {
                Console.WriteLine("        No probing paths specified, skipping...");
                continue;
            }

            foreach (var path in additionalProbingPaths)
            {
                var fullPath = Path.Combine(baseDir, path);
                Console.WriteLine($"        Got probing path: {fullPath}");
                probePaths.Add(fullPath);
            }
        }

        Console.WriteLine("Using probing paths (in order):");
        foreach (var path in probePaths)
        {
            Console.WriteLine($"    {path}");
        }

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var asmName = new AssemblyName(args.Name).Name ?? args.Name;
            if (assemblies.TryGetValue(asmName, out var assembly))
            {
                return assembly;
            }

            var runtimeLib = depContext.RuntimeLibraries.FirstOrDefault(x => x.Name == asmName);
            if (runtimeLib is null)
            {
                return null;
            }

            foreach (var asmGroup in runtimeLib.RuntimeAssemblyGroups)
            {
                foreach (var runtimeFile in asmGroup.RuntimeFiles)
                {
                    foreach (var probePath in probePaths)
                    {
                        string[] potentialPaths = runtimeLib.Path is null
                            ? [Path.Combine(probePath, Path.Combine(runtimeLib.Name, runtimeLib.Version), runtimeFile.Path), Path.Combine(probePath, runtimeFile.Path)]
                            : [Path.Combine(probePath, runtimeLib.Path, runtimeFile.Path)];

                        foreach (var potentialPath in potentialPaths)
                        {
                            if (!File.Exists(potentialPath))
                            {
                                continue;
                            }

                            Console.WriteLine("Resolver: Attempting to load assembly: " + potentialPath);

                            try
                            {
                                var loadedAsm = Assembly.LoadFrom(potentialPath);
                                assemblies[loadedAsm.GetName().Name ?? loadedAsm.FullName ?? potentialPath] = loadedAsm;

                                if (loadedAsm.GetName().Name == asmName)
                                {
                                    return assemblies[asmName] = loadedAsm;
                                }
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine($"Failed to load assembly ({potentialPath}): {e}");
                            }
                        }
                    }
                }
            }

            return null;
        };

        // With everything set up, this should automatically resolve.
        return Assembly.Load(assemblyName);
    }
}
