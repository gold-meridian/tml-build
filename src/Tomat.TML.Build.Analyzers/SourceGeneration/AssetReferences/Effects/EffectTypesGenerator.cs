using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Tomat.TML.Build.Analyzers.SourceGeneration;

/// <summary>
///     Generates common type definitions used for the effect asset generator.
/// </summary>
[Generator]
public sealed class EffectTypesGenerator : IIncrementalGenerator
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
                    "EffectTypes.g.cs",
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

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal interface IShaderParameters
              {
                  void Apply(EffectParameterCollection parameters, string passName);
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
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

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal readonly struct HlslVoid;

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal readonly struct HlslString;

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              [global::System.AttributeUsage(global::System.AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
              internal sealed class OriginalHlslTypeAttribute(string hlslType) : global::System.Attribute
              {
                  public string HlslType => hlslType;
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal struct HlslSampler
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal struct HlslSampler1D
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal struct HlslSampler2D
              {
                  public Texture2D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal struct HlslSampler3D
              {
                  public Texture3D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]
              internal struct HlslSamplerCube
              {
                  public TextureCube? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }
              """;
    }
}
