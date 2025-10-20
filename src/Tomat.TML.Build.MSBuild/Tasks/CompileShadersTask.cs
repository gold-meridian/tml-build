using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Tomat.Parsing.Diagnostics;
using Tomat.TML.Build.Common.Assets.Effects;

namespace Tomat.TML.Build.MSBuild.Tasks;

public sealed class CompileShadersTask : BaseTask
{
    [Required]
    public required string[] CompilationUnits { get; set; } = [];

    [Required]
    public required string NativesDir { get; set; } = string.Empty;

    [Required]
    public required string ProjectDirectory { get; set; }

    protected override bool Run()
    {
        var diagnostics = new DiagnosticsCollection();

        if (!Directory.Exists(NativesDir))
        {
            diagnostics.AddError(
                origin: "Shader compiler",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: $"Could not find natives directory: {NativesDir}"
            );
            return Log.ReportDiagnostics(diagnostics);
        }

        var fxcExe = Path.Combine(NativesDir, "fxc.exe");
        if (!File.Exists(fxcExe))
        {
            diagnostics.AddError(
                origin: "Shader compiler",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: $"Could not find fxc.exe: {fxcExe}"
            );
            return Log.ReportDiagnostics(diagnostics);
        }

        var fxcExePath = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "/bin/bash",
                Arguments = "-c \"command -v wine\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var otherProcess = new Process();
            otherProcess.StartInfo = processStartInfo;
            otherProcess.Start();

            var error = otherProcess.StandardError.ReadToEnd();
            var output = otherProcess.StandardOutput.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                diagnostics.AddError(
                    origin: "Shader compiler (WINE)",
                    location: DiagnosticLocation.NO_LOCATION,
                    code: "SHADERC",
                    message: error
                );
                return Log.ReportDiagnostics(diagnostics);
            }

            if (!string.IsNullOrEmpty(output))
            {
                fxcExePath = fxcExe;
                fxcExe = output.Trim();
            }
            else
            {
                diagnostics.AddError(
                    origin: "Shader compiler (WINE)",
                    location: DiagnosticLocation.NO_LOCATION,
                    code: "SHADERC",
                    message: "WINE not found; maybe try installing it from your package manager?"
                );
                return Log.ReportDiagnostics(diagnostics);
            }
        }

        foreach (var compilationUnit in Utilities.PrependDirectoryToUnrootedPaths(CompilationUnits, ProjectDirectory))
        {
            CompileShader(fxcExePath, fxcExe, compilationUnit, diagnostics);
        }

        return Log.ReportDiagnostics(diagnostics);
    }

    private static void CompileShader(string fxcExePath, string fxcExe, string filePath, DiagnosticsCollection diagnostics)
    {
        var fxcOutput = Path.ChangeExtension(filePath, ".fxc");

        // Skip building if we can assume the shader is up-to-date.
        if (!CPreprocessor.IsOutOfDate(filePath, fxcOutput))
        {
            return;
        }

        var pInfo = new ProcessStartInfo
        {
            FileName = fxcExe,
            Arguments = $"{fxcExePath} /T fx_2_0 \"{CheckLinuxPathConversion(filePath, diagnostics)}\" /Fo \"{CheckLinuxPathConversion(fxcOutput, diagnostics)}\" /D FX=1 /O3 /Op",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process();
        process.StartInfo = pInfo;
        process.OutputDataReceived += (_, e) => PrintInfo(e.Data, filePath);
        process.ErrorDataReceived += (_, e) => PrintError(e.Data, filePath);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0 || (process.ExitCode == -1 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)))
        {
            return;
        }

        diagnostics.AddError(
            origin: filePath,
            location: DiagnosticLocation.NO_LOCATION,
            code: "SHADERC",
            message: $"fxc.exe exited with code {process.ExitCode}"
        );
        return;

        static string CheckLinuxPathConversion(string str, DiagnosticsCollection diagnostics)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return str;
            }

            var process = new Process();
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "/bin/bash",
                Arguments = $"-c \"winepath --windows '{str}'\"", // user already has confirmed wine on path or through package manager
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            process.StartInfo = processStartInfo;
            process.Start();

            var error = process.StandardError.ReadToEnd();
            var output = process.StandardOutput.ReadToEnd();

            if (!string.IsNullOrEmpty(output))
            {
                return output.Trim();
            }

            diagnostics.AddError(
                origin: "Shader compiler (WINEPATH):",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: error
            );
            return str;
        }
    }

    // TODO: Parse these into reportable diagnostics!

    private static void PrintInfo(string? message, string filePath)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (IgnorableMessage(message))
        {
            return;
        }

        Console.WriteLine(message);
    }

    private static void PrintError(string? message, string filePath)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (IgnorableMessage(message))
        {
            return;
        }

        if (message.StartsWith("warning "))
        {
            Console.WriteLine($"{filePath}: {message}");
            return;
        }

        if (message.Contains(": warning "))
        {
            Console.WriteLine(message);
            return;
        }

        if (message.StartsWith("error "))
        {
            Console.Error.WriteLine($"{filePath}: {message}");
            return;
        }

        if (message.Contains(": error "))
        {
            Console.Error.WriteLine(message);
            return;
        }

        Console.Error.WriteLine($"{filePath}: error SHADERC: {message}");
    }

    private static bool IgnorableMessage(string message)
    {
        // Ignore warning X4717 "Effects deprecated for D3DCompiler_47"
        if (message.Contains("X4717") && message.Contains("Effects deprecated for D3DCompiler_47"))
        {
            return true;
        }

        if (message.Contains("fixme"))
        {
            return true;
        }

        // Ignore annoying startup messages about copyright.
        if (message.StartsWith("Microsoft (R)") || message.StartsWith("Copyright (C)"))
        {
            return true;
        }

        // Ignore save success messages.
        if (message.StartsWith("compilation object save succeeded"))
        {
            return true;
        }

        return false;
    }
}
