namespace Tomat.TML.Build.Analyzers.SourceGeneration;

/// <summary>
///     Represents a known range of variants.
/// </summary>
/// <param name="Start">The start of the range.</param>
/// <param name="End">The end of the range.</param>
public readonly record struct VariantData(int Start, int End)
{
    /// <summary>
    ///     The total length of the range; that is, the number of assets
    ///     included within it.
    /// </summary>
    public int Count => End - Start + 1;
}

/// <summary>
///     Represents a path to an asset on disk.
/// </summary>
/// <param name="fullPath">The full path to the asset.</param>
/// <param name="relativePath">
///     The relative (from the designated mod root) path to the asset.
/// </param>
public sealed class AssetPath(string fullPath, string? relativePath)
{
    /// <summary>
    ///     The full path to the asset.
    /// </summary>
    public string FullPath => fullPath;

    /// <summary>
    ///     The relative path to the asset.
    /// </summary>
    public string? RelativePath { get; set; } = relativePath;

    /// <summary>
    ///     The relative path to the asset, unless it is <see langword="null"/>,
    ///     in which case the full path is used instead.
    /// </summary>
    public string RelativeOrFullPath => RelativePath ?? FullPath;
}

/// <summary>
///     An asset (or group of assets, see <see cref="VariantData"/>) which
///     exists on disk, containing an associated <see cref="Generator"/> and
///     known <see cref="Variants"/>.
/// </summary>
/// <param name="Name">The name of the asset.</param>
/// <param name="Path">The path to the asset relative to the mod root.</param>
/// <param name="Generator">
///     The generator used to generate a reference to it in code.
/// </param>
/// <param name="Variants">Known variant range, if applicable.</param>
public readonly record struct AssetFile(
    string Name,
    AssetPath Path,
    IAssetGenerator Generator,
    VariantData? Variants = null
);

/// <summary>
///     Handles generating code references to assets from a path.
/// </summary>
public interface IAssetGenerator
{
    /// <summary>
    ///     Whether the asset at the <paramref name="path"/> is eligible for
    ///     generation using this generator.
    /// </summary>
    /// <param name="path">The path to the asset.</param>
    /// <returns></returns>
    bool Eligible(AssetPath path);
    
    /// <summary>
    ///     Whether the asset at the given path is valid for containing
    ///     variants.  For example, a texture generator may permit all textures
    ///     to be considered for variant generation, but a sound generator may
    ///     only permit sound effects and not music.
    /// </summary>
    /// <param name="path">The path to the asset.</param>
    /// <returns>
    ///     Whether this asset qualifies to be considered for variant
    ///     consideration.
    /// </returns>
    bool PermitsVariant(string path);

    /// <summary>
    ///     Generates code referencing the <paramref name="asset"/>.
    /// </summary>
    /// <param name="assemblyName">
    ///     The assembly name of the project, corresponding to the mod name.
    /// </param>
    /// <param name="asset">The asset file.</param>
    /// <param name="indent">The indentation level.</param>
    /// <returns>Formatted C# code referencing the asset.</returns>
    string GenerateCode(string assemblyName, AssetFile asset, string indent);
}
