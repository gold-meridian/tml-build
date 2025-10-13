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

    // Use the earliest possible value to ensure the latest write time is never
    // overwritten.  This is for ignore or error cases.
    private static readonly DateTime time_ignore = DateTime.MinValue;

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

        // Very naive attempt at resolving imports:
        // - does not handle cases for #include's path is on another line,
        // - probably does not handle path traversal incredibly well,
        // - does not support angle-bracket syntax (unsure if HLSL supports this
        //   at all)
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
