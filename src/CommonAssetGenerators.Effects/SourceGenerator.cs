using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.Effects;

[Generator]
public sealed class SourceGenerator : AssetReferencesGenerator
{
    public override IAssetGenerator[] Generators { get; } = [new EffectGenerator()];
}
