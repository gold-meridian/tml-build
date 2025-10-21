using System;
using System.Text.RegularExpressions;
using Hjson;
using Tomat.Parsing.Diagnostics;

namespace Tomat.TML.Build.Common.Assets.Localization;

/// <summary>
///     Validates <c>.hjson</c> files.
/// </summary>
public static class HjsonValidator
{
    private static readonly Regex error_regex = new(@"(.*?) At line (.*?), column (.*?) \((.*?)\)", RegexOptions.Compiled);

    /// <summary>
    ///     Gets the first found HJSON parsing error.
    /// </summary>
    /// <param name="filePath">The file to parse for errors.</param>
    /// <param name="diagnostics">
    ///     The diagnostics collection to report to.
    /// </param>
    /// <returns>
    ///     The first error, formatted richly for logging; or,
    ///     <see langword="null" />, if there are no errors.
    /// </returns>
    public static void GetNearestErrorInHjsonFile(string filePath, DiagnosticsCollection diagnostics)
    {
        try
        {
            _ = HjsonValue.Load(filePath);
        }
        catch (Exception e)
        {
            diagnostics.Add(GetRichErrorMessage(filePath, e.Message));
        }
    }

    private static ReportableDiagnostic GetRichErrorMessage(string filePath, string message)
    {
        var match = error_regex.Match(message);
        if (!match.Success)
        {
            return ReportableDiagnostic.Error(
                origin: filePath,
                location: DiagnosticLocation.NO_LOCATION,
                code: "HJSON",
                message: message
            );
        }

        var line = match.Groups[2].Value;
        var column = match.Groups[3].Value;
        return ReportableDiagnostic.Error(
            origin: filePath,
            location: DiagnosticLocation.FromLineWithColumn(int.Parse(line), int.Parse(column)),
            code: "HJSON",
            message: match.Groups[1].Value
        );
    }
}
