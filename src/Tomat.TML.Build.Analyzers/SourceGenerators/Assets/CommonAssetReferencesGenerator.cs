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
                  void Apply(EffectParameterCollection parameters, string passName);
              }

              [global::System.Runtime.CompilerServices.CompilerGenerated]
              internal sealed class WrapperShaderData<TParameters>(Asset<Effect> shader, string passName) : ShaderData(shader, passName)
                  where TParameters : IShaderParameters, new()
              {
                  public TParameters Parameters { get; } = new();
                  
                  // Avoid CS9107
                  private readonly string passName = passName;

                  public override void Apply()
                  {
                      Parameters.Apply(Shader.Parameters, passName);

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

              internal sealed class HlslSampler
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal sealed class HlslSampler1D
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal sealed class HlslSampler2D
              {
                  public Texture2D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal sealed class HlslSampler3D
              {
                  public Texture3D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal sealed class HlslSamplerCube
              {
                  public TextureCube? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }
              """;
    }
}
