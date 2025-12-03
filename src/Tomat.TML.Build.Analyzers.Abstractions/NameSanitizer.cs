using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Tomat.TML.Build.Analyzers;

/// <summary>
///     Handles processing arbitrary user input keys and producing valid C#
///     names for type generation.
/// </summary>
public static class NameSanitizer
{
    private static readonly Regex invalid_identifier_chars = new(@"[^\p{L}\p{Nl}\p{Nd}_]", RegexOptions.Compiled);
    private static readonly Regex multi_underscore = new("_+", RegexOptions.Compiled);

    private const int max_identifier_length = 200;

    private static readonly HashSet<string> csharp_keywords = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    /// <summary>
    ///     Transforms an arbitrary, <paramref name="raw"/> input into a legal,
    ///     C#-style identifier.
    ///     <br />
    ///     May not be unique, use <see cref="MakeUniqueIdentifier"/> to guarantee
    ///     that an identifier is unique within a set of ancestral paths.
    /// </summary>
    public static string ToValidIdentifier(string raw)
    {
        raw = raw.Normalize(NormalizationForm.FormKD);

        var normalized = invalid_identifier_chars.Replace(raw, "_");
        normalized = multi_underscore.Replace(normalized, "_");
        normalized = normalized.Trim('_');

        if (normalized.Length > 0 && char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }
        else if (string.IsNullOrEmpty(normalized))
        {
            normalized = "_";
        }

        // For annoyingly-long names.
        if (normalized.Length > max_identifier_length)
        {
            normalized = normalized[..max_identifier_length];
        }

        if (csharp_keywords.Contains(normalized))
        {
            normalized = '_' + normalized;
        }

        return normalized;
    }

    /// <summary>
    ///     Generates a unique identifier given a <paramref name="rawLeaf"/>.
    /// </summary>
    /// <param name="rawLeaf">
    ///     The final path part to generate a name from.
    /// </param>
    /// <param name="pathSegments">
    ///     The fully-split set of preceding path names.
    /// </param>
    /// <param name="usedNames">
    ///     The set of names already used in the scope.  This set may be
    ///     mutated as needed.
    /// </param>
    /// <param name="ancestors">
    ///     Normalized ancestor identifiers which must be avoided.
    /// </param>
    /// <returns></returns>
    public static string MakeUniqueIdentifier(
        string rawLeaf,
        IReadOnlyList<string> pathSegments,
        HashSet<string> usedNames,
        IReadOnlyList<string> ancestors
    )
    {
        var baseName = ToValidIdentifier(rawLeaf);

        if (!NameAppearsInAncestors(baseName, ancestors) && usedNames.Add(baseName))
        {
            return baseName;
        }
        
        // First, try suffixing with ancestor tails.
        // base, base_parent, base_parent_grandparent
        var parentParts = new List<string>();
        for (var i = pathSegments.Count - 2; i >= 0; i--)
        {
            var part = ToValidIdentifier(pathSegments[i]);
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }
            
            parentParts.Add(part);

            var candidate = $"{baseName}_{string.Join("_", parentParts)}";

            if (candidate.Length > max_identifier_length)
            {
                candidate = candidate[..max_identifier_length];
            }

            if (!NameAppearsInAncestors(candidate, ancestors) && usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        // If we somehow can't get away with suffixing ancestors, just add
        // numeric suffixes.
        for (var i = 1; i <= 1000; i++)
        {
            var numericCandidate = $"{baseName}_{i}";

            if (numericCandidate.Length > max_identifier_length)
            {
                numericCandidate = numericCandidate[..max_identifier_length];
            }

            if (!NameAppearsInAncestors(numericCandidate, ancestors) && usedNames.Add(numericCandidate))
            {
                return numericCandidate;
            }
        }

        // Give up?!
        return baseName;
    }

    private static bool NameAppearsInAncestors(string name, IReadOnlyList<string> ancestors)
    {
        if (ancestors.Count == 0)
        {
            return false;
        }

        foreach (var ancestor in ancestors)
        {
            if (string.Equals(ancestor, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
