using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tomat.TML.Build.Analyzers;

internal static class SyntaxHelpers
{
    public static AttributeSyntax GetCompilerGeneratedAttribute()
    {
        return Attribute(NameFromIdentifiers("System", "Runtime", "CompilerServices", "CompiledGeneratedAttribute"));
    }

    public static NameSyntax NameFromIdentifiers(params string[] identifiers)
    {
        if (identifiers.Length == 0)
        {
            throw new InvalidOperationException("Cannot create qualified name with no inputs.");
        }

        if (identifiers.Length == 1)
        {
            return IdentifierName(identifiers[0]);
        }

        NameSyntax rootIdentifier = IdentifierName(identifiers[0]);
        for (var i = 1; i < identifiers.Length; i++)
        {
            rootIdentifier = QualifiedName(rootIdentifier, IdentifierName(identifiers[i]));
        }

        return rootIdentifier;
    }
}
