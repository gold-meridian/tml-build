using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tomat.Parsing.Diagnostics;

namespace Tomat.TML.Build.Common.Assets.Effects;

public static class ShaderCompiler
{
    public static void CompileShader(
        string fxcExePath,
        string fxcExe,
        string filePath,
        DiagnosticsCollection diagnostics
    )
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
        process.OutputDataReceived += (_, e) => LogMessage(e.Data, filePath, diagnostics);
        process.ErrorDataReceived += (_, e) => LogErrorOrWarning(e.Data, filePath, diagnostics);

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

            if (!string.IsNullOrWhiteSpace(output))
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

    private static void LogMessage(string? message, string filePath, DiagnosticsCollection diagnostics)
    {
        if (string.IsNullOrWhiteSpace(message) || message is null)
        {
            return;
        }

        if (IsMessageIgnorable(message))
        {
            return;
        }

        var diag = CanonicalError.Parse(message);
        if (!diag.HasValue)
        {
            diag = new ReportableDiagnostic(
                Origin: filePath,
                Location: DiagnosticLocation.NO_LOCATION,
                Subcategory: null,
                Category: DiagnosticKind.MessageNormal,
                Code: "SHADERC",
                HelpKeyword: null,
                HelpLink: null,
                Message: message
            );
        }
        else if (string.IsNullOrWhiteSpace(diag.Value.Origin))
        {
            diag = diag.Value with { Origin = filePath };
        }

        diagnostics.Add(diag.Value);
    }

    private static void LogErrorOrWarning(string? message, string filePath, DiagnosticsCollection diagnostics)
    {
        if (string.IsNullOrWhiteSpace(message) || message is null)
        {
            return;
        }

        if (IsMessageIgnorable(message))
        {
            return;
        }

        var diag = CanonicalError.Parse(message);
        if (diag.HasValue)
        {
            if (string.IsNullOrWhiteSpace(diag.Value.Origin))
            {
                diag = diag.Value with { Origin = filePath };
            }

            diagnostics.Add(diag.Value);
            return;
        }

        // TODO: Do these all parse correctly now?
        /*
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
        */

        diagnostics.AddError(
            origin: filePath,
            location: DiagnosticLocation.NO_LOCATION,
            code: "SHADERC",
            message: message
        );
    }

    private static bool IsMessageIgnorable(string message)
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
