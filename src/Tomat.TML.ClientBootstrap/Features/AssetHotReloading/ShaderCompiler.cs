using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tomat.Parsing.Diagnostics;
using Tomat.TML.Build.Common.Assets.Effects;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

// Mostly copied from our existing CompileShadersTask

internal static class Shaderc
{
    public static bool Compile(in GameLogWrapper log, string? nativesDir, string shaderFile)
    {
        var diagnostics = new DiagnosticsCollection();

        if (nativesDir is null)
        {
            diagnostics.AddError(
                origin: "Shader compiler",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: "Natives directory is not set"
            );
            return ReportDiagnostics(log, diagnostics);
        }

        if (!Directory.Exists(nativesDir))
        {
            diagnostics.AddError(
                origin: "Shader compiler",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: $"Could not find natives directory: {nativesDir}"
            );
            return ReportDiagnostics(log, diagnostics);
        }

        var fxcExe = Path.Combine(nativesDir, "fxc.exe");
        if (!File.Exists(fxcExe))
        {
            diagnostics.AddError(
                origin: "Shader compiler",
                location: DiagnosticLocation.NO_LOCATION,
                code: "SHADERC",
                message: $"Could not find fxc.exe: {fxcExe}"
            );
            return ReportDiagnostics(log, diagnostics);
        }

        var fxcExePath = "";
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                return ReportDiagnostics(log, diagnostics);
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
                return ReportDiagnostics(log, diagnostics);
            }
        }

        ShaderCompiler.CompileShader(fxcExePath, fxcExe, shaderFile, diagnostics);
        return ReportDiagnostics(in log, diagnostics);
    }

    private static bool ReportDiagnostics(in GameLogWrapper log, DiagnosticsCollection diagnostics)
    {
        foreach (var diag in diagnostics)
        {
            ReportDiagnostic(in log, diag);
        }

        return !diagnostics.HasErrors;
    }

    private static void ReportDiagnostic(in GameLogWrapper log, in ReportableDiagnostic diagnostic)
    {
        var message = FormatMsBuildDiagnostic(diagnostic);
        switch (diagnostic.Category)
        {
            case DiagnosticKind.Error:
                log.Error(message);
                break;

            case DiagnosticKind.Warning:
                log.Warn(message);
                break;

            default:
                log.Info(message);
                break;
        }
    }

    private static string FormatMsBuildDiagnostic(in ReportableDiagnostic d)
    {
        var sb = new StringBuilder(256);

        // Origin (file)
        if (!string.IsNullOrEmpty(d.Origin))
        {
            sb.Append(d.Origin);

            if (d.Location != DiagnosticLocation.NO_LOCATION)
            {
                var loc = d.Location /*.WithZeroIndexedLinesAndColumns()*/;

                sb.Append('(');

                sb.Append(loc.LineStart);

                if (loc.ColumnStart > 1)
                {
                    sb.Append(',');
                    sb.Append(loc.ColumnStart);
                }

                sb.Append(')');
            }

            sb.Append(": ");
        }

        // Subcategory (optional)
        if (!string.IsNullOrEmpty(d.Subcategory))
        {
            sb.Append(d.Subcategory);
            sb.Append(' ');
        }

        // Kind
        sb.Append(
            d.Category switch
            {
                DiagnosticKind.Error => "error",
                DiagnosticKind.Warning => "warning",
                _ => "message",
            }
        );

        sb.Append(' ');

        // Code
        if (!string.IsNullOrEmpty(d.Code))
        {
            sb.Append(d.Code);
            sb.Append(": ");
        }

        // Message
        if (!string.IsNullOrEmpty(d.Message))
        {
            if (d.MessageArgs is { Length: > 0 })
            {
                sb.AppendFormat(d.Message, d.MessageArgs);
            }
            else
            {
                sb.Append(d.Message);
            }
        }

        // Optional help link (MSBuild sometimes supports this as trailing text)
        /*
        if (!string.IsNullOrEmpty(d.HelpLink))
        {
            sb.Append(" [");
            sb.Append(d.HelpLink);
            sb.Append(']');
        }
        */

        return sb.ToString();
    }
}
