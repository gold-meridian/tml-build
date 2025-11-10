using System;
using System.Collections.Generic;

namespace Tomat.TML.Build.Analyzers;

/// <summary>
///     Provides resolved asset generators.
/// </summary>
public static class AssetGeneratorProvider
{
    private static readonly Dictionary<Type, IAssetGenerator> generators = [];

    /// <summary>
    ///     An enumerable collection of known asset generators.
    /// </summary>
    public static IEnumerable<IAssetGenerator> KnownGenerators => generators.Values;

    /// <summary>
    ///     Adds a generator of type <typeparamref name="TAssetGenerator"/> to
    ///     a list of known generators (to be used for asset generation).
    /// </summary>
    /// <typeparam name="TAssetGenerator">The asset generator.</typeparam>
    public static void AddGenerator<TAssetGenerator>()
        where TAssetGenerator : IAssetGenerator, new()
    {
        AddGenerator(new TAssetGenerator());
    }

    /// <summary>
    ///     Adds the <paramref name="generator"/> to a list of known generators
    ///     (to be used for asset generation).
    /// </summary>
    /// <param name="generator">The asset generator.</param>
    public static void AddGenerator(IAssetGenerator generator)
    {
        // TODO: Complain?
        if (generators.ContainsKey(generator.GetType()))
        {
            return;
        }

        generators.Add(generator.GetType(), generator);
    }
}
