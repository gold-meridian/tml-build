using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
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
            ShaderCompiler.CompileShader(fxcExePath, fxcExe, compilationUnit, diagnostics);
        }

        return Log.ReportDiagnostics(diagnostics);
    }
}
