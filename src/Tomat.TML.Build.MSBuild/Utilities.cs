using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Tomat.Parsing.Diagnostics;

namespace Tomat.TML.Build.MSBuild;

internal static class Utilities
{
    public static IEnumerable<string> PrependDirectoryToUnrootedPaths(IEnumerable<string> paths, string rootDir)
    {
        foreach (var path in paths)
        {
            if (!Path.IsPathRooted(path))
            {
                yield return Path.Combine(rootDir, path);
            }
            else
            {
                yield return path;
            }
        }
    }

    public static bool ReportDiagnostics(
        this TaskLoggingHelper log,
        DiagnosticsCollection diagnostics
    )
    {
        foreach (var diag in diagnostics)
        {
            ReportDiagnostic(log, diag);
        }

        return !diagnostics.HasErrors;
    }

    public static void ReportDiagnostic(
        this TaskLoggingHelper log,
        ReportableDiagnostic diagnostic
    )
    {
        switch (diagnostic.Category)
        {
            case DiagnosticKind.Error:
                log.LogError(
                    subcategory: diagnostic.Subcategory,
                    errorCode: diagnostic.Code,
                    helpKeyword: diagnostic.HelpKeyword,
                    helpLink: diagnostic.HelpLink,
                    file: diagnostic.Origin,
                    lineNumber: diagnostic.Location.LineStart,
                    columnNumber: diagnostic.Location.ColumnStart,
                    endLineNumber: diagnostic.Location.LineEnd,
                    endColumnNumber: diagnostic.Location.ColumnEnd,
                    message: diagnostic.Message,
                    messageArgs: diagnostic.MessageArgs
                );
                break;

            case DiagnosticKind.Warning:
                log.LogWarning(
                    subcategory: diagnostic.Subcategory,
                    warningCode: diagnostic.Code,
                    helpKeyword: diagnostic.HelpKeyword,
                    helpLink: diagnostic.HelpLink,
                    file: diagnostic.Origin,
                    lineNumber: diagnostic.Location.LineStart,
                    columnNumber: diagnostic.Location.ColumnStart,
                    endLineNumber: diagnostic.Location.LineEnd,
                    endColumnNumber: diagnostic.Location.ColumnEnd,
                    message: diagnostic.Message,
                    messageArgs: diagnostic.MessageArgs
                );
                break;

            case DiagnosticKind.MessageLow:
            case DiagnosticKind.MessageNormal:
            case DiagnosticKind.MessageHigh:
                // lol why doesn't it have a help link
                log.LogMessage(
                    subcategory: diagnostic.Subcategory,
                    code: diagnostic.Code,
                    helpKeyword: diagnostic.HelpKeyword,
                    /*helpLink: diagnostic.HelpLink,*/
                    file: diagnostic.Origin,
                    lineNumber: diagnostic.Location.LineStart,
                    columnNumber: diagnostic.Location.ColumnStart,
                    endLineNumber: diagnostic.Location.LineEnd,
                    endColumnNumber: diagnostic.Location.ColumnEnd,
                    importance: diagnostic.Category switch
                    {
                        DiagnosticKind.MessageLow => MessageImportance.Low,
                        DiagnosticKind.MessageNormal => MessageImportance.Normal,
                        DiagnosticKind.MessageHigh => MessageImportance.High,
                        _ => MessageImportance.Low,
                    },
                    message: diagnostic.Message,
                    messageArgs: diagnostic.MessageArgs
                );
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
