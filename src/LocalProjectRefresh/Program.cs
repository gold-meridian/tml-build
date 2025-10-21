using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LocalProjectRefresh.LockFinding;

namespace LocalProjectRefresh;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Known NuGet directories to force-delete: {args[0]}");
        }

        RunCommand("dotnet", "clean Tomat.TML.TestMod");
        RunCommand("dotnet", "nuget delete -s local Tomat.Terraria.ModLoader.Sdk 1.0.0 --non-interactive");
        RunCommand("dotnet", "nuget delete -s local Tomat.TML.Build.Analyzers 1.0.0 --non-interactive");
        foreach (var dir in args[0].Split(';'))
        {
            ForceDeleteDirectory(dir.Trim());
        }
        RunCommand("dotnet", "build Tomat.TML.Build.MSBuild -c Release");
        RunCommand("dotnet", "build Tomat.TML.Build.Analyzers -c Release");
        RunCommand("dotnet", "build Tomat.TML.ClientBootstrap -c Release");
        RunCommand("dotnet", "build Tomat.Terraria.ModLoader.Sdk -c Release");
        RunCommand("dotnet", "nuget push Tomat.TML.Build.Analyzers/bin/Release/Tomat.TML.Build.Analyzers.1.0.0.nupkg -s local");
        RunCommand("dotnet", "nuget push Tomat.Terraria.ModLoader.Sdk/bin/Release/Tomat.Terraria.ModLoader.Sdk.1.0.0.nupkg -s local");
        RunCommand("dotnet", "restore Tomat.TML.TestMod");
    }

    private static void RunCommand(string program, string arguments)
    {
        Console.WriteLine($"> {program} {arguments}");

        var startInfo = new ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            Console.Write("    ");
            Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            Console.Write("    ");
            Console.Error.WriteLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"!!! Command failed with exit code: {process.ExitCode}");
        Console.ResetColor();
    }

    private static void ForceDeleteDirectory(string dir)
    {
        Console.WriteLine($"# Force-deleting directory (kills processes with handles): {dir}");

        if (!Directory.Exists(dir))
        {
            Console.WriteLine("    Directory does not exist.");
            return;
        }

        if (File.Exists(dir))
        {
            Console.Error.WriteLine("    !!!! Found file in place of directory.");
            return;
        }

        Console.WriteLine("    Attempting to delete files normally...");

        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception e)
        {
            Console.WriteLine($"    Caught error when deleting normally: {e}");
        }

        Console.WriteLine("    Checking if directory stile exists...");

        if (!Directory.Exists(dir))
        {
            Console.WriteLine("    Directory successfully deleted!");
            return;
        }

        Console.WriteLine("    Directory still exists, attempting to kill processes...");

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("    This operation is only supported on Windows; aborting...");
            return;
        }

        var paths = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                             .Distinct()
                             .ToArray();

        Console.WriteLine("    Checking for handles on the following paths:");
        foreach (var path in paths)
        {
            Console.WriteLine($"        - \"{path}\"");
        }

        var totalProcesses = new HashSet<ProcessInfo>();
        foreach (var path in paths)
        {
            Console.WriteLine($"    Checking \"{path}\":");

            var processes = LockFinder.FindWhatProcessesLockPath(path).ToArray();
            Console.WriteLine($"        Got processes: {processes.Length}");

            totalProcesses.UnionWith(processes);
        }

        Console.WriteLine($"    Got {totalProcesses.Count} total processes, attempting to kill...");
        foreach (var process in totalProcesses)
        {
            Console.Write($"        Killing process: PID({process.Pid})... ");

            try
            {
                var proc = Process.GetProcessById(process.Pid);
                proc.Kill(entireProcessTree: true);
                Console.WriteLine("SUCCESS");
            }
            catch (Exception e)
            {
                Console.WriteLine("FAIL");
                Console.Error.WriteLine($"Failed to kill process PID({process.Pid}): {e}");
            }
        }
    }
}
