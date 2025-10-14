using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Tomat.TML.Build.MSBuild.Tasks;

public sealed class PackageModTask : BaseTask
{
    // Possible packages to include.
    public ITaskItem[] PackageReferences { get; set; } = [];

    // Possible projects to include.
    public ITaskItem[] ProjectReferences { get; set; } = [];

    // Possible assemblies to include.
    public TaskItem[] ReferencePaths { get; set; } = [];

    public string ProjectDirectory { get; set; } = string.Empty;

    public string OutputPath { get; set; } = string.Empty;

    public string AssemblyName { get; set; } = string.Empty;

    public string TmlDllPath { get; set; } = string.Empty;

    public string OutputTmodPath { get; set; } = string.Empty;

    public ITaskItem[] ModProperties { get; set; } = [];

    protected override bool Run()
    {
        return true;
    }
}
