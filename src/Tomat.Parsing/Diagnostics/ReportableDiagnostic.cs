namespace Tomat.Parsing.Diagnostics;

/// <summary>
///     The diagnostic kind.
/// </summary>
public enum DiagnosticKind
{
    /// <summary>
    ///     An error.
    /// </summary>
    Error,

    /// <summary>
    ///     A warning.
    /// </summary>
    Warning,

    /// <summary>
    ///     A message of low importance; not an error or warning.
    /// </summary>
    MessageLow,

    /// <summary>
    ///     A message of normal importance; not an error or warning.
    /// </summary>
    MessageNormal,

    /// <summary>
    ///     A message of high importance; not an error or warning.
    /// </summary>
    MessageHigh,
}

/// <summary>
///     The location of the reported diagnostic.
/// </summary>
/// <param name="LineStart">The line in which the diagnostic begins.</param>
/// <param name="LineEnd">The line in which the diagnostic ends.</param>
/// <param name="ColumnStart">The column of the beginning line.</param>
/// <param name="ColumnEnd">The column of the ending line.</param>
public readonly record struct DiagnosticLocation(
    int LineStart,
    int LineEnd,
    int ColumnStart,
    int ColumnEnd
)
{
    public static readonly DiagnosticLocation NO_LOCATION = new(
        LineStart: 0,
        LineEnd: 0,
        ColumnStart: 0,
        ColumnEnd: 0
    );

#region Factory methods
    // Explicit factory methods because some signatures are identical in
    // constructors.

    public static DiagnosticLocation FromLine(
        int line
    )
    {
        return new DiagnosticLocation(
            LineStart: line,
            LineEnd: line,
            ColumnStart: 0,
            ColumnEnd: 0
        );
    }

    public static DiagnosticLocation FromLineRange(
        int lineStart,
        int lineEnd
    )
    {
        return new DiagnosticLocation(
            LineStart: lineStart,
            LineEnd: lineEnd,
            ColumnStart: lineStart,
            ColumnEnd: lineEnd
        );
    }

    public static DiagnosticLocation FromLineWithColumn(
        int line,
        int column
    )
    {
        return new DiagnosticLocation(
            LineStart: line,
            LineEnd: line,
            ColumnStart: column,
            ColumnEnd: column
        );
    }

    public static DiagnosticLocation FromLineWithColumnRange(
        int line,
        int columnStart,
        int columnEnd
    )
    {
        return new DiagnosticLocation(
            LineStart: line,
            LineEnd: line,
            ColumnStart: columnStart,
            ColumnEnd: columnEnd
        );
    }

    public static DiagnosticLocation FromLineRangeWithColumnRange(
        int lineStart,
        int lineEnd,
        int columnStart,
        int columnEnd
    )
    {
        return new DiagnosticLocation(
            LineStart: lineStart,
            LineEnd: lineEnd,
            ColumnStart: columnStart,
            ColumnEnd: columnEnd
        );
    }
#endregion

#region Transformation helpers
    /// <summary>
    ///     Increments the line range of the diagnostic by 1.
    /// </summary>
    public DiagnosticLocation WithZeroIndexedLines()
    {
        return this with
        {
            LineStart = LineStart + 1,
            LineEnd = LineEnd + 1,
        };
    }

    /// <summary>
    ///     Increments the column range of this diagnostic by 1.
    /// </summary>
    public DiagnosticLocation WithZeroIndexedColumns()
    {
        return this with
        {
            ColumnStart = ColumnStart + 1,
            ColumnEnd = ColumnEnd + 1,
        };
    }

    /// <summary>
    ///     Increments the line and column ranges of this diagnostic by 1.
    /// </summary>
    public DiagnosticLocation WithZeroIndexedLinesAndColumns()
    {
        return WithZeroIndexedLines()
           .WithZeroIndexedColumns();
    }
#endregion
}

/// <summary>
///     A reportable error or warning diagnostic, with all the information
///     MSBuild may report to the user.
/// </summary>
/// <param name="Origin">The origin of the diagnostic, usually a file.</param>
/// <param name="Location">The location of the diagnostic in the file.</param>
/// <param name="Subcategory">Additional category classification.</param>
/// <param name="Category">The diagnostic kind.</param>
/// <param name="Code">The application-specific diagnostic code.</param>
/// <param name="HelpKeyword">The help keyword for documentation.</param>
/// <param name="HelpLink">The help link for documentation.</param>
/// <param name="Message">The message displayed to the user.</param>
/// <param name="MessageArgs">Arguments used to format the message.</param>
public readonly record struct ReportableDiagnostic(
    string Origin,
    DiagnosticLocation Location,
    string? Subcategory,
    DiagnosticKind Category,
    string Code,
    string? HelpKeyword,
    string? HelpLink,
    string? Message,
    params object[] MessageArgs
)
{
    /// <summary>
    ///     Whether this diagnostic should be treated as an error.
    /// </summary>
    public bool IsError => Category == DiagnosticKind.Error;

    public ReportableDiagnostic WithHelpInfo(
        string? helpKeyword,
        string? helpLink
    )
    {
        return this with
        {
            HelpKeyword = helpKeyword,
            HelpLink = helpLink,
        };
    }

#region Factory methods
    public static ReportableDiagnostic Error(
        string origin,
        DiagnosticLocation location,
        string? subcategory,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        return new ReportableDiagnostic(
            Origin: origin,
            Location: location,
            Subcategory: subcategory,
            Category: DiagnosticKind.Error,
            Code: code,
            HelpKeyword: null,
            HelpLink: null,
            Message: message,
            MessageArgs: messageArgs
        );
    }

    public static ReportableDiagnostic Error(
        string origin,
        DiagnosticLocation location,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        return new ReportableDiagnostic(
            Origin: origin,
            Location: location,
            Subcategory: null,
            Category: DiagnosticKind.Error,
            Code: code,
            HelpKeyword: null,
            HelpLink: null,
            Message: message,
            MessageArgs: messageArgs
        );
    }

    public static ReportableDiagnostic Warning(
        string origin,
        DiagnosticLocation location,
        string? subcategory,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        return new ReportableDiagnostic(
            Origin: origin,
            Location: location,
            Subcategory: subcategory,
            Category: DiagnosticKind.Warning,
            Code: code,
            HelpKeyword: null,
            HelpLink: null,
            Message: message,
            MessageArgs: messageArgs
        );
    }

    public static ReportableDiagnostic Warning(
        string origin,
        DiagnosticLocation location,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        return new ReportableDiagnostic(
            Origin: origin,
            Location: location,
            Subcategory: null,
            Category: DiagnosticKind.Warning,
            Code: code,
            HelpKeyword: null,
            HelpLink: null,
            Message: message,
            MessageArgs: messageArgs
        );
    }
#endregion
}

public static class ReportableDiagnosticDiagnosticsCollectionExtensions
{
    public static void AddError(
        this DiagnosticsCollection diagnostics,
        string origin,
        DiagnosticLocation location,
        string? subcategory,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        diagnostics.Add(
            ReportableDiagnostic.Error(
                origin,
                location,
                subcategory,
                code,
                message,
                messageArgs
            )
        );
    }

    public static void AddError(
        this DiagnosticsCollection diagnostics,
        string origin,
        DiagnosticLocation location,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        diagnostics.Add(
            ReportableDiagnostic.Error(
                origin,
                location,
                code,
                message,
                messageArgs
            )
        );
    }

    public static void AddWarning(
        this DiagnosticsCollection diagnostics,
        string origin,
        DiagnosticLocation location,
        string? subcategory,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        diagnostics.Add(
            ReportableDiagnostic.Warning(
                origin,
                location,
                subcategory,
                code,
                message,
                messageArgs
            )
        );
    }

    public static void AddWarning(
        this DiagnosticsCollection diagnostics,
        string origin,
        DiagnosticLocation location,
        string code,
        string? message,
        params object[] messageArgs
    )
    {
        diagnostics.Add(
            ReportableDiagnostic.Warning(
                origin,
                location,
                code,
                message,
                messageArgs
            )
        );
    }
}
