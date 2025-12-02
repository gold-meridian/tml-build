using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Tomat.TML.Build.Analyzers.SourceGenerators;

/// <summary>
///     Generates a default <c>ModImpl</c> definition extending
///     <c>Terraria.ModLoader.Mod</c>.
/// </summary>
[Generator]
public sealed class ModImplGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = GeneratorsHelper.GetRootNamespaceOrAssemblyName(
            context.AnalyzerConfigOptionsProvider,
            context.CompilationProvider
        );

        var enabledProvider = context.AnalyzerConfigOptionsProvider.Select(
            static (config, _) =>
                config.GlobalOptions.TryGetValue("build_property.TmlBuildUseDefaultModImpl", out var enabled)
             && enabled == "true"
        );

        /*
        // Find any class declarations and check if they extend
        // `Terraria.ModLoader.Mod`.  If they do, filter to only true cases and
        // collect into an array, then return whether that array has any
        // elements.  This should adequately determine if a mod implements the
        // base Mod class themselves.
        var alreadyHasModImpl = context.SyntaxProvider.CreateSyntaxProvider(
            static (n, _) => n is ClassDeclarationSyntax { BaseList: { } baseSyntax },
            static (n, _) =>
            {
                if (n.SemanticModel.GetDeclaredSymbol(n.Node) is not ITypeSymbol typeSymbol)
                {
                    return false;
                }

                if (typeSymbol.BaseType is not { } baseType)
                {
                    return false;
                }

                return baseType.ContainingNamespace.ToString() == "Terraria.ModLoader"
                    && baseType.Name == "Mod";
            }
        ).Where(x => x).Collect().Select((x, _) => x.Length > 0);
        */

        context.RegisterSourceOutput(
            rootNamespaceProvider.Combine(enabledProvider),
            (ctx, tuple) =>
            {
                var (rootNamespace, enabled) = tuple;
                if (!enabled)
                {
                    return;
                }

                ctx.AddSource(
                    "ModImpl.g.cs",
                    SourceText.From(GenerateModImpl(rootNamespace), Encoding.UTF8)
                );
            }
        );
    }

    private static string GenerateModImpl(string rootNamespace)
    {
        return
            $$"""
              #nullable enable

              namespace {{rootNamespace}};

              [global::JetBrains.Annotations.UsedImplicitly(global::JetBrains.Annotations.ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              public sealed partial class ModImpl : global::Terraria.ModLoader.Mod;
              """;
    }
}
