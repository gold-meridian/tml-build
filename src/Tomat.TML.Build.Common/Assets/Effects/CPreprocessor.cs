using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Tomat.TML.Build.Common.Assets.Effects;

/// <summary>
///     Extremely naive parser to handle some useful preprocessor directives.
///     <br />
///     Does not aim for accuracy or correctness, only used to optimize some
///     compilation cases.
/// </summary>
public static class CPreprocessor
{
    private static readonly Regex include_regex = new(
        """
        ^\s*#\s*include\s*"([^"]+)"
        """,
        RegexOptions.Compiled
    );

    // Use the earliest possible value to ensure the latest write time is never
    // overwritten.  This is for ignore or error cases.
    private static readonly DateTime time_ignore = DateTime.MinValue;

    /// <summary>
    ///     Whether a given compilation unit is out-of-date with its compiled
    ///     binary.
    /// </summary>
    public static bool IsOutOfDate(string compilationUnitPath, string binaryPath)
    {
        // Always out-of-date if there is no compiled object.
        if (!File.Exists(binaryPath))
        {
            return true;
        }

        // Traverse includes to determine the latest change made to any of the
        // text within a compilation unit.
        var compilationUnitTime = GetLatestWriteTime(compilationUnitPath);
        var binaryWriteTime = File.GetLastWriteTimeUtc(binaryPath);
        return compilationUnitTime > binaryWriteTime;
    }

    private static DateTime GetLatestWriteTime(string filePath)
    {
        return GetLatestWriteTimeRecursive(Path.GetFullPath(filePath), []);
    }

    private static DateTime GetLatestWriteTimeRecursive(string filePath, HashSet<string> visitedFiles)
    {
        if (visitedFiles.Contains(filePath))
        {
            return time_ignore;
        }

        if (!File.Exists(filePath))
        {
            return time_ignore;
        }

        visitedFiles.Add(filePath);

        var latest = File.GetLastWriteTimeUtc(filePath);

        // Very naive attempt at resolving imports, certainly does not handle
        // all cases.
        // TODO: Attempt to improve correctness of parsing #include statements?
        // https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-appendix-pre-include
        //
        // "": 1. in the same directory as the file that contains the #include
        //        directive.
        //     2. in the directories of any files that contain a #include
        //        directive for the file that contains the #include directive.
        //     3. in paths specified by the /I compiler option, in the order in
        //        which they are listed.
        //     4. in paths specified by the INCLUDE environment variable, in the
        //        order in which they are listed.
        // <>: 1. in paths specified by the /I compiler option, in the order in
        //        which they are listed.
        //     2. in paths specified by the INCLUDE environment variable, in the
        //        order in which they are listed.
        //
        // NOTE: The INCLUDE environment variable is ignored in an development
        //       environment. Refer to your development environment's
        //       documentation for information about how to set the include
        //       paths for your project.
        //
        // The above is not directrly implementable since we do not have the
        // compiler context, but we can do our best to match the behavior that
        // is replicable.
        var lines = File.ReadAllLines(filePath);
        var baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;

        foreach (var line in lines)
        {
            var match = include_regex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var includePath = match.Groups[1].Value;
            var fullIncludePath = Path.Combine(baseDir, includePath);

            var includeWriteTime = GetLatestWriteTimeRecursive(fullIncludePath, visitedFiles);
            if (includeWriteTime > latest)
            {
                latest = includeWriteTime;
            }
        }

        return latest;
    }
}
