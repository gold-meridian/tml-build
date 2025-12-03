using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.SourceGeneration;

[Generator]
public sealed class DefaultAssetReferencesGenerator : AssetReferencesGenerator
{
    public override IAssetGenerator[] Generators { get; } =
    [
        new TextureGenerator(),
        new SoundGenerator(),
        new EffectGenerator(),
    ];
}
