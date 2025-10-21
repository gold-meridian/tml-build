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
              namespace {{rootNamespace}};

              [global::JetBrains.Annotations.UsedImplicitly(global::JetBrains.Annotations.ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
              [global::System.Runtime.CompilerServices.CompilerGenerated]
              public sealed partial class ModImpl : global::Terraria.ModLoader.Mod;
              """;
    }
}
