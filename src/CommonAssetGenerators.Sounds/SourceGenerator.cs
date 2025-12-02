using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.Sounds;

[Generator]
public sealed class SourceGenerator : AssetReferencesGenerator
{
    public override IAssetGenerator[] Generators { get; } = [new SoundGenerator()];
}
