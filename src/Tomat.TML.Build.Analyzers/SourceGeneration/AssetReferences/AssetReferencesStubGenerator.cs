using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.SourceGeneration;

[Generator]
public sealed class AssetReferencesStubGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(
        IncrementalGeneratorInitializationContext context
    )
    {
        var rootNamespaceProvider = AdditionalValueProviders.GetRootNamespaceOrAssemblyName(
            context.AnalyzerConfigOptionsProvider,
            context.CompilationProvider
        );

        context.RegisterSourceOutput(
            rootNamespaceProvider,
            (ctx, rootNamespace) =>
            {
                ctx.AddSource(
                    "AssetReferences.g.cs",
                    GenerateStub(rootNamespace)
                );
            }
        );
    }

    private static string GenerateStub(string rootNamespace)
    {
        return
            $"""
             #nullable enable
             #pragma warning disable CS8981

             global using static {rootNamespace}.Core.AssetReferences;

             namespace {rootNamespace}.Core;

             // ReSharper disable InconsistentNaming
             [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
             internal static partial class AssetReferences;
             """;
    }
}
