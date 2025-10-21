using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Tomat.Parsing.Diagnostics;

// Replicated from:
// https://github.com/dotnet/msbuild/blob/main/src/Shared/CanonicalError.cs

/// <summary>
///     Functions for dealing with the specially formatted errors returned by
///     build tools.
/// </summary>
/// <remarks>
///     Various tools produce and consume CanonicalErrors in various formats.
///     DEVENV Format When Clicking on Items in the Output Window
///     (taken from env\msenv\core\findutil.cpp ParseLocation function)
///     v:\dir\file.ext (loc) : msg
///     \\server\share\dir\file.ext(loc):msg
///     url
///     loc:
///     (line)
///     (line-line)
///     (line,col)
///     (line,col-col)
///     (line,col,len)
///     (line,col,line,col)
///     DevDiv Build Process
///     (taken from tools\devdiv2.def)
///     To echo warnings and errors to the build console, the
///     "description block" must be recognized by build. To do this,
///     add a $(ECHO_COMPILING_COMMAND) or $(ECHO_PROCESSING_COMMAND)
///     to the first line of the description block, e.g.
///     $(ECHO_COMPILING_CMD) Resgen_$&lt;
///     Errors must have the format:
///     &lt;text&gt; : error [num]: &lt;msg&gt;
///     Warnings must have the format:
///     &lt;text&gt; : warning [num]: &lt;msg&gt;
/// </remarks>
public static class CanonicalError
{
    // Defines the main pattern for matching messages.
    private const string origin_category_code_text_expression_pattern =
        // Beginning of line and any amount of whitespace.
        @"^\s*"
        // Match a [optional project number prefix 'ddd>'], single letter + colon + remaining filename, or
        // string with no colon followed by a colon.
      + @"(((?<ORIGIN>(((\d+>)?[a-zA-Z]?:[^:]*)|([^:]*))):)"
        // Origin may also be empty. In this case there's no trailing colon.
      + "|())"
        // Match the empty string or a string without a colon that ends with a space
      + "(?<SUBCATEGORY>(()|([^:]*? )))"
        // Match 'error' or 'warning'.
      + "(?<CATEGORY>(error|warning))"
        // Match anything starting with a space that's not a colon/space, followed by a colon.
        // Error code is optional in which case "error"/"warning" can be followed immediately by a colon.
      + @"( \s*(?<CODE>[^: ]*))?\s*:"
        // Whatever's left on this line, including colons.
      + "(?<TEXT>.*)$";

    private const string origin_category_code_text_expression2_pattern =
        @"^\s*(?<ORIGIN>(?<FILENAME>.*):(?<LOCATION>(?<LINE>[0-9]*):(?<COLUMN>[0-9]*))):(?<CATEGORY> error| warning):(?<TEXT>.*)";

    // Matches and extracts filename and location from an 'origin' element.
    private const string filename_location_from_origin_pattern =
        "^"                         // Beginning of line
      + @"(\d+>)?"                  // Optional ddd> project number prefix
      + "(?<FILENAME>.*)"           // Match anything.
      + @"\("                       // Find a parenthesis.
      + @"(?<LOCATION>[\,,0-9,-]*)" // Match any combination of numbers and ',' and '-'
      + @"\)\s*"                    // Find the closing paren then any amount of spaces.
      + "$";                        // End-of-line

    // Matches location that is a simple number.
    private const string line_from_location_pattern = // Example: line
        "^"                                           // Beginning of line
      + "(?<LINE>[0-9]*)"                             // Match any number.
      + "$";                                          // End-of-line

    // Matches location that is a range of lines.
    private const string line_line_from_location_pattern = // Example: line-line
        "^"                                                // Beginning of line
      + "(?<LINE>[0-9]*)"                                  // Match any number.
      + "-"                                                // Dash
      + "(?<ENDLINE>[0-9]*)"                               // Match any number.
      + "$";                                               // End-of-line

    // Matches location that is a line and column
    private const string line_col_from_location_pattern = // Example: line,col
        "^"                                               // Beginning of line
      + "(?<LINE>[0-9]*)"                                 // Match any number.
      + ","                                               // Comma
      + "(?<COLUMN>[0-9]*)"                               // Match any number.
      + "$";                                              // End-of-line

    // Matches location that is a line and column-range
    private const string line_col_col_from_location_pattern = // Example: line,col-col
        "^"                                                   // Beginning of line
      + "(?<LINE>[0-9]*)"                                     // Match any number.
      + ","                                                   // Comma
      + "(?<COLUMN>[0-9]*)"                                   // Match any number.
      + "-"                                                   // Dash
      + "(?<ENDCOLUMN>[0-9]*)"                                // Match any number.
      + "$";                                                  // End-of-line

    // Matches location that is line,col,line,col
    private const string line_col_line_col_from_location_pattern = // Example: line,col,line,col
        "^"                                                        // Beginning of line
      + "(?<LINE>[0-9]*)"                                          // Match any number.
      + ","                                                        // Comma
      + "(?<COLUMN>[0-9]*)"                                        // Match any number.
      + ","                                                        // Dash
      + "(?<ENDLINE>[0-9]*)"                                       // Match any number.
      + ","                                                        // Dash
      + "(?<ENDCOLUMN>[0-9]*)"                                     // Match any number.
      + "$";                                                       // End-of-line

    private const RegexOptions regex_options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

    private static Regex? originCategoryCodeTextExpression;
    private static Regex? originCategoryCodeTextExpression2;
    private static Regex? filenameLocationFromOrigin;
    private static Regex? lineFromLocation;
    private static Regex? lineLineFromLocation;
    private static Regex? lineColFromLocation;
    private static Regex? lineColColFromLocation;
    private static Regex? lineColLineColFromLocation;

    private static readonly char[] single_quote_char = ['\''];

    private static Regex OriginCategoryCodeTextExpression =>
        originCategoryCodeTextExpression ??= new Regex(origin_category_code_text_expression_pattern, regex_options);

    private static Regex OriginCategoryCodeTextExpression2 =>
        originCategoryCodeTextExpression2 ??= new Regex(origin_category_code_text_expression2_pattern, regex_options);

    private static Regex FilenameLocationFromOrigin =>
        filenameLocationFromOrigin ??= new Regex(filename_location_from_origin_pattern, regex_options);

    private static Regex LineFromLocation =>
        lineFromLocation ??= new Regex(line_from_location_pattern, regex_options);

    private static Regex LineLineFromLocation =>
        lineLineFromLocation ??= new Regex(line_line_from_location_pattern, regex_options);

    private static Regex LineColFromLocation =>
        lineColFromLocation ??= new Regex(line_col_from_location_pattern, regex_options);

    private static Regex LineColColFromLocation =>
        lineColColFromLocation ??= new Regex(line_col_col_from_location_pattern, regex_options);

    private static Regex LineColLineColFromLocation =>
        lineColLineColFromLocation ??= new Regex(line_col_line_col_from_location_pattern, regex_options);

    /// <summary>
    ///     A small custom int conversion method that treats invalid entries as missing (0). This is done to work around tools
    ///     that don't fully conform to the canonical message format - we still want to salvage what we can from the message.
    /// </summary>
    /// <param name="value"></param>
    /// <returns>'value' converted to int or 0 if it can't be parsed or is negative</returns>
    private static int ConvertToIntWithDefault(string value)
    {
        var success = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);

        if (!success || (result < 0))
        {
            result = DiagnosticLocation.NO_LOCATION.LineStart;
        }

        return result;
    }

    /// <summary>
    ///     Decompose an error or warning message into constituent parts. If the message isn't in the canonical form, return null.
    /// </summary>
    /// <remarks>This method is thread-safe, because the Regex class is thread-safe (per MSDN).</remarks>
    /// <param name="message"></param>
    /// <returns>Decomposed canonical message, or null.</returns>
    public static ReportableDiagnostic? Parse(string message)
    {
        // An unusually long string causes pathologically slow Regex back-tracking.
        // To avoid that, only scan the first 400 characters. That's enough for
        // the longest possible prefix: MAX_PATH, plus a huge subcategory string, and an error location.
        // After the regex is done, we can append the overflow.
        var messageOverflow = string.Empty;
        if (message.Length > 400)
        {
            messageOverflow = message[400..];
            message = message[..400];
        }

        // If a tool has a large amount of output that isn't an error or warning (e.g., "dir /s %hugetree%")
        // the regex below is slow. It's faster to pre-scan for "warning" and "error"
        // and bail out if neither are present.
        if (message.IndexOf("warning", StringComparison.OrdinalIgnoreCase) < 0 &&
            message.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        // ReSharper disable RedundantAssignment
        var diagOrigin = string.Empty;
        var diagLocation = DiagnosticLocation.NO_LOCATION;
        var diagSubcategory = default(string?);
        var diagCategory = DiagnosticKind.Error;
        var diagCode = string.Empty;
        var diagText = default(string?);
        // ReSharper restore RedundantAssignment

        // First, split the message into three parts--Origin, Category, Code, Text.
        // Example,
        //      Main.cs(17,20):Command line warning CS0168: The variable 'foo' is declared but never used
        //      -------------- ------------ ------- ------  ----------------------------------------------
        //      Origin         SubCategory  Cat.    Code    Text
        //
        // To accommodate absolute filenames in Origin, tolerate a colon in the second position
        // as long as it's preceded by a letter.
        //
        // Localization Note:
        //  Even in foreign-language versions of tools, the category field needs to be in English.
        //  Also, if origin is a tool name, then that needs to be in English.
        //
        //  Here's an example from the Japanese version of CL.EXE:
        //   cl : ???? ??? warning D4024 : ?????????? 'AssemblyInfo.cs' ?????????????????? ???????????
        //
        //  Here's an example from the Japanese version of LINK.EXE:
        //   AssemblyInfo.cpp : fatal error LNK1106: ???????????? ??????????????: 0x6580 ??????????
        //
        var match = OriginCategoryCodeTextExpression.Match(message);
        string category;
        if (!match.Success)
        {
            // try again with the Clang/GCC matcher
            // Example,
            //       err.cpp:6:3: error: use of undeclared identifier 'force_an_error'
            //       -----------  -----  ---------------------------------------------
            //       Origin       Cat.   Text
            match = OriginCategoryCodeTextExpression2.Match(message);
            if (!match.Success)
            {
                return null;
            }

            category = match.Groups["CATEGORY"].Value.Trim();
            if (string.Equals(category, "error", StringComparison.OrdinalIgnoreCase))
            {
                diagCategory = DiagnosticKind.Error;
            }
            else if (string.Equals(category, "warning", StringComparison.OrdinalIgnoreCase))
            {
                diagCategory = DiagnosticKind.Warning;
            }
            else
            {
                // Not an error\warning message.
                return null;
            }

            diagLocation = DiagnosticLocation.FromLineWithColumn(
                ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim()),
                ConvertToIntWithDefault(match.Groups["COLUMN"].Value.Trim())
            );
            diagText = (match.Groups["TEXT"].Value + messageOverflow).Trim();
            diagOrigin = match.Groups["FILENAME"].Value.Trim();

            var explodedText = diagText.Split(single_quote_char, StringSplitOptions.RemoveEmptyEntries);
            diagCode = explodedText.Length > 0 ? $"G{explodedText[0].GetHashCode():X8}" : "G00000000";

            return new ReportableDiagnostic(
                Origin: diagOrigin,
                Location: diagLocation,
                Subcategory: diagSubcategory,
                Category: diagCategory,
                Code: diagCode,
                HelpKeyword: null,
                HelpLink: null,
                Message: diagText
            );
        }

        var origin = match.Groups["ORIGIN"].Value.Trim();
        category = match.Groups["CATEGORY"].Value.Trim();
        diagCode = match.Groups["CODE"].Value.Trim();
        diagText = (match.Groups["TEXT"].Value + messageOverflow).Trim();
        diagSubcategory = match.Groups["SUBCATEGORY"].Value.Trim();

        // Next, see if category is something that is recognized.
        if (string.Equals(category, "error", StringComparison.OrdinalIgnoreCase))
        {
            diagCategory = DiagnosticKind.Error;
        }
        else if (string.Equals(category, "warning", StringComparison.OrdinalIgnoreCase))
        {
            diagCategory = DiagnosticKind.Warning;
        }
        else
        {
            // Not an error\warning message.
            return null;
        }

        // Origin is not a simple file, but it still could be of the form,
        //  foo.cpp(location)
        match = FilenameLocationFromOrigin.Match(origin);

        if (match.Success)
        {
            // The origin is in the form,
            //  foo.cpp(location)
            // Assume the filename exists, but don't verify it. What else could it be?
            var location = match.Groups["LOCATION"].Value.Trim();
            diagOrigin = match.Groups["FILENAME"].Value.Trim();

            if (location.Length <= 0)
            {
                return new ReportableDiagnostic(
                    Origin: diagOrigin,
                    Location: diagLocation,
                    Subcategory: diagSubcategory,
                    Category: diagCategory,
                    Code: diagCode,
                    HelpKeyword: null,
                    HelpLink: null,
                    Message: diagText
                );
            }

            // Now, take apart the location. It can be one of these:
            //      loc:
            //      (line)
            //      (line-line)
            //      (line,col)
            //      (line,col-col)
            //      (line,col,len)
            //      (line,col,line,col)

            match = LineFromLocation.Match(location);
            if (match.Success)
            {
                diagLocation = DiagnosticLocation.FromLine(
                    line: ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim())
                );
            }
            else
            {
                match = LineLineFromLocation.Match(location);

                if (match.Success)
                {
                    diagLocation = DiagnosticLocation.FromLineRange(
                        lineStart: ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim()),
                        lineEnd: ConvertToIntWithDefault(match.Groups["ENDLINE"].Value.Trim())
                    );
                }
                else
                {
                    match = LineColFromLocation.Match(location);

                    if (match.Success)
                    {
                        diagLocation = DiagnosticLocation.FromLineWithColumn(
                            line: ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim()),
                            column: ConvertToIntWithDefault(match.Groups["COLUMN"].Value.Trim())
                        );
                    }
                    else
                    {
                        match = LineColColFromLocation.Match(location);

                        if (match.Success)
                        {
                            diagLocation = DiagnosticLocation.FromLineWithColumnRange(
                                line: ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim()),
                                columnStart: ConvertToIntWithDefault(match.Groups["COLUMN"].Value.Trim()),
                                columnEnd: ConvertToIntWithDefault(match.Groups["ENDCOLUMN"].Value.Trim())
                            );
                        }
                        else
                        {
                            match = LineColLineColFromLocation.Match(location);

                            if (match.Success)
                            {
                                diagLocation = DiagnosticLocation.FromLineRangeWithColumnRange(
                                    lineStart: ConvertToIntWithDefault(match.Groups["LINE"].Value.Trim()),
                                    lineEnd: ConvertToIntWithDefault(match.Groups["ENDLINE"].Value.Trim()),
                                    columnStart: ConvertToIntWithDefault(match.Groups["COLUMN"].Value.Trim()),
                                    columnEnd: ConvertToIntWithDefault(match.Groups["ENDCOLUMN"].Value.Trim())
                                );
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // The origin does not fit the filename(location) pattern.
            diagOrigin = origin;
        }

        return new ReportableDiagnostic(
            Origin: diagOrigin,
            Location: diagLocation,
            Subcategory: diagSubcategory,
            Category: diagCategory,
            Code: diagCode,
            HelpKeyword: null,
            HelpLink: null,
            Message: diagText
        );
    }
}
