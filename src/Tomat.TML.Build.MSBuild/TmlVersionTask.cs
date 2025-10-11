using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.MSBuild;

public sealed class TmlVersionTask : Task
{
    [Required]
    public required string TmlBuildPath { get; set; }

    [Required]
    public required string Version { get; set; }

    [Required]
    public required string TargetsPath { get; set; }

    public override bool Execute()
    {
        Log.LogMessage("Using tML version (unparsed): {0}", Version);

        // check if the process exited with a non-zero exit code
        if (!VersionExecution.DownloadVersion(Version, new Logger(Log)))
        {
            Log.LogError($"Failed to download requested tML version in task, aborting: {Version}");
            return false;
        }

        if (VersionExecution.GetVersionPath(Version, new Logger(Log)) is not { } path)
        {
            Log.LogError("Failed to get the path of tML version {0}.", Version);
            return false;
        }

        Log.LogMessage("Got tML version path: {0}", path);

        var targetsCandidates = new[]
        {
            Path.Combine(path, "tMLMod.targets"),
            Path.Combine(path, "Build", "tMLMod.targets"),
        };

        var targets = targetsCandidates.FirstOrDefault(File.Exists);
        if (targets == null)
        {
            Log.LogError("Failed to find tML targets file in {0}.", path);
            return false;
        }

        // write a dummy targets file that imports the real targets file
        Directory.CreateDirectory(Path.GetDirectoryName(TargetsPath)!);
        Log.LogMessage(TargetsPath);
        File.WriteAllText(
            TargetsPath,
            $"""
             <Project>
                 <Import Project="{targets}"/>
             </Project>
             """
        );

        return true;
    }
}
