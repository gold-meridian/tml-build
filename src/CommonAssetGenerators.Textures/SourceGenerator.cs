using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.Textures;

[Generator]
public sealed class SourceGenerator : AssetReferencesGenerator
{
    public override IAssetGenerator[] Generators { get; } = [new TextureGenerator()];
}
