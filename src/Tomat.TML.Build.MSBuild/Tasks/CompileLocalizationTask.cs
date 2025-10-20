using Microsoft.Build.Framework;
using Tomat.Parsing.Diagnostics;
using Tomat.TML.Build.Common.Assets.Localization;

namespace Tomat.TML.Build.MSBuild.Tasks;

public sealed class CompileLocalizationTask : BaseTask
{
    [Required]
    public required string[] CompilationUnits { get; set; } = [];

    [Required]
    public required string ProjectDirectory { get; set; }

    protected override bool Run()
    {
        var diagnostics = new DiagnosticsCollection();

        foreach (var compilationUnit in Utilities.PrependDirectoryToUnrootedPaths(CompilationUnits, ProjectDirectory))
        {
            HjsonValidator.GetNearestErrorInHjsonFile(compilationUnit, diagnostics);
        }

        return Log.ReportDiagnostics(diagnostics);
    }
}
