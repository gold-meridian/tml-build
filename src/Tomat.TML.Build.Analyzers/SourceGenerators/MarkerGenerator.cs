using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tomat.TML.Build.Analyzers.SourceGenerators;

/// <summary>
///     Generates a definite marker indicating a given assembly (usually a mod)
///     was built against this analyzer.
///     <br />
///     This serves a canonical watermarking purpose to indicate a build tool
///     was used (both for any farther analysis and debugging), but also to
///     ensure that a mod always has at least one type in its assembly under a
///     namespace that is exactly equal to the mod's internal name (the assembly
///     name).
///     <br />
///     This relieves a naive restriction built into tModLoader implemented due
///     to in-game building assuming a mod's internal name is the same as its
///     parent directory.
/// </summary>
[Generator]
public sealed class MarkerGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(
        IncrementalGeneratorInitializationContext context
    )
    {
        var assemblyNameProvider = context.CompilationProvider.Select((compilation, _) => compilation.AssemblyName);

        context.RegisterImplementationSourceOutput(
            assemblyNameProvider,
            (ctx, assemblyName) =>
            {
                if (assemblyName is null)
                {
                    throw new InvalidOperationException("Cannot generate build marker for compilation without assembly name");
                }

                var fileText =
                    $$"""
                      namespace {{assemblyName}};

                      [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
                      file static class __TmlBuildMarker
                      {
                          // TODO: Many save the version and stuff here?
                      }
                      """;

                ctx.AddSource(
                    "TmlBuildAnalysisMarker.g.cs",
                    SourceText.From(fileText, Encoding.UTF8)
                );
            }
        );
    }
}
