using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
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

        var probePaths = new List<string> { baseDir };
        var runtimeConfigs = new[] { rcPath, rcDevPath };
        Console.WriteLine("Finding probing paths...");
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

        var resolver = new AssemblyResolver(depsPath, probePaths.ToArray());
        var nativeFiles = resolver.GetNativeFiles().ToArray();
        var nativeDirs = nativeFiles.Select(x => new FileInfo(x).Directory).Where(x => x is not null).Distinct();

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => resolver.ResolveAssembly(new AssemblyName(args.Name));
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, s) =>
        {
            foreach (var nativeDir in nativeDirs)
            {
                if (nativeDir is null || !nativeDir.Exists)
                {
                    continue;
                }

                foreach (var nativeFile in GetNativeFileNames(s))
                {
                    var nativePath = Path.Combine(nativeDir.FullName, nativeFile);
                    if (!File.Exists(nativePath))
                    {
                        continue;
                    }

                    if (NativeLibrary.TryLoad(nativePath, out var handle))
                    {
                        return handle;
                    }
                }
            }

            return 0;
        };

        // With everything set up, this should automatically resolve.
        return Assembly.Load(assemblyName);
    }

    private static readonly string[] windows_native_formats =
    [
        "{0}",
        "{0}.dll",
    ];

    private static readonly string[] unix_native_formats =
    [
        "{0}.{1}",
        "lib{0}.{1}",
        "{0}",
        "lib{0}",
    ];

    private static readonly string[] linux_so_native_formats =
    [
        "{0}",
        "lib{0}",
        "{0}.so",
        "lib{0}.so",
    ];

    private static IEnumerable<string> GetNativeFileNames(string fileName)
    {
        if (OperatingSystem.IsWindows())
        {
            if (fileName.EndsWith(".dll"))
            {
                yield return fileName;
            }
            else
            {
                foreach (var windowsFmt in windows_native_formats)
                {
                    yield return string.Format(windowsFmt, fileName);
                }
            }

            yield break;
        }

        var isMac = OperatingSystem.IsMacOS();
        var isLinux = OperatingSystem.IsLinux();
        if (!isMac && !isLinux)
        {
            yield break;
        }

        var ext = isMac ? "dylib" : "so";

        if (isLinux && (fileName.EndsWith(".so") || fileName.Contains(".so.")))
        {
            foreach (var soFmt in linux_so_native_formats)
            {
                yield return string.Format(soFmt, fileName);
            }

            yield break;
        }

        foreach (var unixFmt in unix_native_formats)
        {
            yield return string.Format(unixFmt, fileName, ext);
        }
    }
}
