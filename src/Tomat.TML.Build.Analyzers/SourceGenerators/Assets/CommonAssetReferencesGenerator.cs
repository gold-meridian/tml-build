using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;
using System.Xml.Linq;

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

                ctx.AddSource(
                    "ObjModelTypes.g.cs",
                    SourceText.From(GenerateObjModelTypes(rootNamespace), Encoding.UTF8)
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

              internal struct HlslSampler
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal struct HlslSampler1D
              {
                  public Texture? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal struct HlslSampler2D
              {
                  public Texture2D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal struct HlslSampler3D
              {
                  public Texture3D? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }

              internal struct HlslSamplerCube
              {
                  public TextureCube? Texture { get; set; }
                  
                  public SamplerState? Sampler { get; set; }
              }
              """;
    }

    private static string GenerateObjModelTypes(string rootNamespace)
    {
        return
            $$"""
              #nullable enable
              
              using Microsoft.Xna.Framework;
              using Microsoft.Xna.Framework.Graphics;
              using System;
              using System.Collections.Generic;
              using System.IO;
              using Terraria;
              using Terraria.ModLoader;
              
              namespace {{rootNamespace}}.Core;
              
              [global::System.Runtime.CompilerServices.CompilerGenerated]
              public record struct Mesh(string Name, int StartIndex, int EndIndex) : IDisposable
              {
                  public VertexBuffer? Buffer { get; set; }

                  public VertexBuffer? SetBuffer<T>(GraphicsDevice device, T[] vertices) where T : struct, IVertexType
                  {
                      if (Buffer is not null && !Buffer.IsDisposed)
                          return Buffer;

                      if (vertices.Length < 3)
                          throw new InvalidOperationException($"{nameof(Mesh)}: Not enough vertices to generate {nameof(VertexBuffer)}!");

                      Buffer = new(device, typeof(T), EndIndex - StartIndex, BufferUsage.None);
                      Buffer.SetData(vertices, StartIndex, EndIndex - StartIndex);

                      return Buffer;
                  }

                  public readonly void Dispose()
                  {
                      Buffer?.Dispose();
                  }
              }
              
              [global::System.Runtime.CompilerServices.CompilerGenerated]
              public class ObjModel : IDisposable
              {
                  private VertexPositionNormalTexture[]? Vertices;

                  private Mesh[]? Meshes;

                  private VertexBuffer? ResetBuffer(GraphicsDevice device, int i)
                  {
                      if (Vertices is null || Meshes is null || !Meshes.IndexInRange(i))
                      {
                          return null;
                      }

                      return Meshes[i].ResetBuffer(device, Vertices);
                  }

                  private void ResetBuffers(GraphicsDevice device)
                  {
                      if (Vertices is null || Meshes is null)
                      {
                          return;
                      }

                      Array.ForEach(Meshes, m => m.ResetBuffer(device, Vertices));
                  }

                  public void Dispose()
                  {
                      if (Meshes is not null)
                      {
                          Array.ForEach(Meshes, m => m.Dispose());
                      }
                  }

                  public static ObjModel Create(Stream stream)
                  {
                      ObjModel model = new();

                      List<VertexPositionNormalTexture> vertices = [];

                      List<Mesh> meshes = [];

                      List<Vector3> positions = [];
                      List<Vector2> textureCoordinates = [];
                      List<Vector3> vertexNormals = [];

                      string meshName = string.Empty;
                      int startIndex = 0;

                      bool containsNonTriangularFaces = false;

                      using StreamReader reader = new(stream);

                      string? text;

                      while ((text = reader.ReadLine()) is not null)
                      {
                          string[] segments = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                          if (segments.Length == 0)
                          {
                              continue;
                          }

                          switch (segments[0])
                          {
                              case "o":
                                  if (segments.Length < 2)
                                      break;

                                  if (vertices.Count > 3 && meshName != string.Empty)
                                      meshes.Add(new Mesh(meshName, startIndex, vertices.Count));

                                  meshName = segments[1];
                                  startIndex = vertices.Count;
                                  break;

                              case "v":
                                  if (segments.Length < 4)
                                      break;

                                  positions.Add(new(
                                      float.Parse(segments[1]), 
                                      float.Parse(segments[2]), 
                                      float.Parse(segments[3])));
                                  break;

                              case "vt":
                                  if (segments.Length < 3)
                                      break;

                                  textureCoordinates.Add(new(
                                      float.Parse(segments[1]),
                                      float.Parse(segments[2])));
                                  break;

                              case "vn":
                                  if (segments.Length < 4)
                                      break;

                                  vertexNormals.Add(new(
                                      float.Parse(segments[1]),
                                      float.Parse(segments[2]),
                                      float.Parse(segments[3])));
                                  break;

                              case "f":
                                  if (segments.Length != 4)
                                  {
                                      containsNonTriangularFaces = true;
                                      break;
                                  }

                                  for (int i = 1; i < segments.Length; i++) 
                                  {
                                      VertexPositionNormalTexture vertex = new();

                                      string[] components = segments[i].Split('/', StringSplitOptions.RemoveEmptyEntries);

                                      if (components.Length != 3)
                                      {
                                          continue;
                                      }

                                      vertex.Position = positions[int.Parse(components[0]) - 1];

                                      Vector2 coord = textureCoordinates[int.Parse(components[1]) - 1];
                                      coord.Y = 1 - coord.Y;

                                      vertex.TextureCoordinate = coord;

                                      Vector3 normal = vertexNormals[int.Parse(components[2]) - 1];
                                      vertex.Normal = normal;

                                      vertices.Add(vertex);
                                  }
                                  break;
                          }
                      }

                      if (vertices.Count > 3 && meshName != string.Empty)
                          meshes.Add(new Mesh(meshName, startIndex, vertices.Count));

                      if (meshes.Count > 0) 
                          model.Meshes = [.. meshes];
                      else
                          throw new InvalidDataException($"{nameof(ObjModel)}: Model did not contain at least one object!");

                      model.Vertices = [.. vertices];

                          // ModContent.GetInstance<{assemblyName}>().Logger.Warn($"{nameof(ObjModel)}: Model contained non triangular faces! These will not be drawn.");

                      if (model.Vertices.Length < 3)
                          throw new InvalidDataException($"{nameof(ObjModel)}: Not enough vertices to create vertex buffer!");

                      model.ResetBuffers(Main.instance.GraphicsDevice);

                      return model;
                  }

                  public void Draw(GraphicsDevice device, string name)
                  {
                      if (Meshes is null)
                      {
                          return;
                      }

                      int i = Array.FindIndex(Meshes, m => m.Name == name);

                      if (i != -1)
                      {
                          Draw(device, i);
                      }
                  }

                  public void Draw(GraphicsDevice device, int i = 0)
                  {
                      VertexBuffer? buffer = ResetBuffer(device, i);

                      if (buffer is null)
                      {
                          return;
                      }

                      device.SetVertexBuffer(buffer);

                      device.DrawPrimitives(PrimitiveType.TriangleList, 0, buffer.VertexCount / 3);
                  }
              }

              /// <summary>
              /// This type must be manually loaded via <see cref=""Mod.AddContent""/> in <see cref=""Mod.CreateDefaultContentSource""/> for .obj models load in-game.<br></br>
              /// <code>
              /// public override IContentSource CreateDefaultContentSource()
              /// {
              ///     if (!Main.dedServ)
              ///     {
              ///         AddContent(new ObjModelReader());
              ///     }
              ///
              ///     return base.CreateDefaultContentSource();
              /// }
              /// </code>
              /// </summary>
              [global::System.Runtime.CompilerServices.CompilerGenerated]
              [Autoload(false)]
              public sealed class ObjModelReader : IAssetReader, ILoadable
              {
                  public static readonly string Extension = ".obj";
                  
                  public void Load(Mod mod)
                  {
                      AssetReaderCollection? assetReaderCollection = Main.instance.Services.Get<AssetReaderCollection>();

                      if (!assetReaderCollection.TryGetReader(Extension, out IAssetReader reader) || reader != this)
                          assetReaderCollection.RegisterReader(this, Extension);
                  }

                  public void Unload() { }

                  public async ValueTask<T> FromStream<T>(Stream stream, MainThreadCreationContext mainThreadCtx) where T : class
                  {
                      if (typeof(T) != typeof(ObjModel))
                          throw AssetLoadException.FromInvalidReader<ObjModelReader, T>();

                      await mainThreadCtx;

                      ObjModel? result = ObjModel.Create(stream);

                      return (result as T)!;
                  }
              }
              """;
    }
}