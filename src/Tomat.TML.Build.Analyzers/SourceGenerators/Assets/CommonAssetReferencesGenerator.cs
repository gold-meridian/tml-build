using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Tomat.TML.Build.Analyzers.SourceGenerators.Assets;

/// <summary>
///     Generates common type definitions used for various asset generators, as
///     well as the using directives to access them ergonomically.
/// </summary>
[Generator]
public sealed class CommonAssetReferencesGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(
        IncrementalGeneratorInitializationContext context
    )
    {
        var rootNamespaceProvider = GeneratorsHelper.GetRootNamespaceOrAssemblyName(
            context.AnalyzerConfigOptionsProvider,
            context.CompilationProvider
        );

        context.RegisterSourceOutput(
            rootNamespaceProvider,
            (ctx, rootNamespace) =>
            {
                ctx.AddSource(
                    "ShaderTypes.g.cs",
                    SourceText.From(GenerateShaderTypes(rootNamespace), Encoding.UTF8)
                );
            }
        );
    }

    private static string GenerateShaderTypes(string rootNamespace)
    {
        return
            $$"""
              #nullable enable

              using Microsoft.Xna.Framework.Graphics;
              using ReLogic.Content;
              using Terraria.Graphics.Shaders;

              namespace {{rootNamespace}}.Core;

              [global::System.Runtime.CompilerServices.CompilerGenerated]
              internal interface IShaderParameters
              {
                  void Apply(EffectParameterCollection parameters);
              }

              [global::System.Runtime.CompilerServices.CompilerGenerated]
              internal sealed class WrapperShaderData<TParameters>(Asset<Effect> shader, string passName) : ShaderData(shader, passName)
                  where TParameters : IShaderParameters, new()
              {
                  public TParameters Parameters { get; } = new();

                  public override void Apply()
                  {
                      Parameters.Apply(Shader.Parameters);

                      base.Apply();
                  }
              }

              [global::System.Runtime.CompilerServices.CompilerGenerated]
              internal readonly struct HlslVoid;

              [global::System.Runtime.CompilerServices.CompilerGenerated]
              internal readonly struct HlslString;

              [global::System.AttributeUsage(global::System.AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
              internal sealed class OriginalHlslTypeAttribute(string hlslType) : global::System.Attribute
              {
                  public string HlslType => hlslType;
              }
              """;
    }
}
