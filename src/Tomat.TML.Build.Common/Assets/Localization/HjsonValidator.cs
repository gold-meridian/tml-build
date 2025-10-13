using System;
using System.Text.RegularExpressions;
using Hjson;

namespace Tomat.TML.Build.Common.Assets.Localization;

/// <summary>
///     Validates <c>.hjson</c> files.
/// </summary>
public static class HjsonValidator
{
    private static readonly Regex error_regex = new(@"(.*?) At line (.*?), column (.*?) \((.*?)\)", RegexOptions.Compiled);

    /// <summary>
    ///     Gets the nearest HJSON parsing error in a file.
    /// </summary>
    /// <param name="filePath">The file to parse for errors.</param>
    /// <returns>
    ///     The nearest error, formatted richly for logging; or,
    ///     <see langword="null"/>, if there are no errors.
    /// </returns>
    public static string? GetNearestErrorInHjsonFile(string filePath)
    {
        try
        {
            _ = HjsonValue.Load(filePath);
            return null;
        }
        catch (Exception e)
        {
            return GetRichErrorMessage(filePath, e.Message);
        }
    }

    private static string GetRichErrorMessage(string filePath, string message)
    {
        var match = error_regex.Match(message);
        if (!match.Success)
        {
            return $"{filePath}: error HJSON: {message}";
        }

        var line = match.Groups[2].Value;
        var column = match.Groups[3].Value;
        return $"{filePath}({line},{column}): error HJSON: {match.Groups[1].Value}";
    }
}
