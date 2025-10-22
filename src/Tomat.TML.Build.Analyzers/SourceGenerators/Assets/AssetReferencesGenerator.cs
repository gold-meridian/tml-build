﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Tomat.TML.Build.Analyzers.SourceGenerators.Assets;

internal readonly record struct VariantData(int Start, int End)
{
    public int Count => End - Start + 1;
}

internal readonly record struct AssetFile(
    string Name,
    AssetPath Path,
    IAssetReference Reference,
    VariantData? Variants = null
);

internal sealed record PathNode(
    string Name,
    Dictionary<string, PathNode> Nodes,
    List<AssetFile> Files
);

/// <summary>
///     Generates strongly-typed references to known asset names.
///     <br />
///     Provides direct access to their value (lazily-loaded), as well as
///     potentially additional type-safe bindings depending on the file type.
/// </summary>
[Generator]
public sealed class AssetReferencesGenerator : IIncrementalGenerator
{
    private static readonly Regex end_number_regex = new(@"([^\d]+)([\d]+)$", RegexOptions.Compiled);
    private static readonly Regex non_alphanumeric = new(@"[^\w]", RegexOptions.Compiled);

    private static readonly char[] number_chars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    private static readonly IAssetReference[] asset_references =
    [
        new TextureReference(),
        new SoundReference(),
        new EffectReference(),
    ];

    void IIncrementalGenerator.Initialize(
        IncrementalGeneratorInitializationContext context
    )
    {
        var rootNamespaceProvider = GeneratorsHelper.GetRootNamespaceOrAssemblyName(
            context.AnalyzerConfigOptionsProvider,
            context.CompilationProvider
        );

        var assemblyNameProvider = context.CompilationProvider.Select(
            static (compilation, _) => compilation.AssemblyName ?? throw new InvalidOperationException("Cannot generate asset references for compilation without assembly name")
        );

        // TODO: Naive, can at least filter some extensions ahead of time?
        var filesProvider = context.AdditionalTextsProvider.Select(
            static (file, _) => new AssetPath(file.Path, relativePath: null)
        );

        var projectDirProvider = context.AnalyzerConfigOptionsProvider.Select(
            static (options, _) => options.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir)
                ? projectDir
                : throw new InvalidOperationException("Cannot generate asset references for compilation without project directory")
        );

        context.RegisterSourceOutput(
            rootNamespaceProvider.Combine(
                assemblyNameProvider.Combine(
                    filesProvider.Collect().Combine(
                        projectDirProvider
                    )
                )
            ),
            static (ctx, tuple) =>
            {
                var (rootNamespace, (assemblyName, (files, projectDir))) = tuple;

                var root = CreateAssetTree(files, projectDir, ctx.CancellationToken);

                ctx.AddSource(
                    "AssetReferences.g.cs",
                    GenerateAssetFile(rootNamespace, assemblyName, root, ctx.CancellationToken)
                );
            }
        );

        return;

        static PathNode CreateAssetTree(
            ImmutableArray<AssetPath> paths,
            string projectDir,
            CancellationToken token
        )
        {
            var rootNode = new PathNode("Root", [], []);

            foreach (var path in paths)
            {
                token.ThrowIfCancellationRequested();

                if (!Path.IsPathRooted(path.FullPath))
                {
                    // Assume relative to project dir already.
                    path.RelativePath = path.FullPath;
                }
                else if (path.FullPath.StartsWith(projectDir))
                {
                    path.RelativePath = path.FullPath[projectDir.Length..];
                }
                else
                {
                    // File is not eligible.
                    // TODO: Look into using the exact same assumptions as TMOD
                    //       packing?
                    continue;
                }

                path.RelativePath = path.RelativePath.Replace('\\', '/');

                if (!Eligible(path, out var reference))
                {
                    continue;
                }

                var pathNodes = path.RelativePath.Split('/');
                var fileName = Path.GetFileNameWithoutExtension(path.RelativeOrFullPath);

                var currentNode = rootNode;
                for (var i = 0; i < pathNodes.Length - 1; i++) // -1 to exclude the file name
                {
                    var directoryName = pathNodes[i];

                    if (!currentNode.Nodes.TryGetValue(directoryName, out var value))
                    {
                        value = new PathNode(directoryName, [], []);
                        currentNode.Nodes[directoryName] = value;
                    }

                    currentNode = value;
                }

                var assetFile = new AssetFile(fileName, path, reference);
                currentNode.Files.Add(assetFile);
            }

            return rootNode;
        }

        static bool Eligible(AssetPath path, out IAssetReference reference)
        {
            foreach (var assetReference in asset_references)
            {
                if (!assetReference.Eligible(path))
                {
                    continue;
                }

                reference = assetReference;
                return true;
            }

            reference = null!;
            return false;
        }
    }

    private static string GenerateAssetFile(
        string rootNamespace,
        string assemblyName,
        PathNode root,
        CancellationToken token
    )
    {
        return
            $$"""
              #nullable enable
              #pragma warning disable CS8981

              global using static {{rootNamespace}}.Core.AssetReferences;

              namespace {{rootNamespace}}.Core;

              // ReSharper disable InconsistentNaming
              internal static class AssetReferences
              {
              {{GenerateTextFromPathNode(assemblyName, root, token)}}
              }
              """;

        static string GenerateTextFromPathNode(
            string assemblyName,
            PathNode root,
            CancellationToken token,
            int depth = 0
        )
        {
            token.ThrowIfCancellationRequested();

            var sb = new StringBuilder();

            var indent = new string(' ', depth * 4);

            if (depth != 0)
            {
                sb.AppendLine($"{indent}public static class {NormalizeName(root.Name)}");
                sb.AppendLine($"{indent}{{");
            }

            var seenVariantBases = new HashSet<string>();

            for (var i = 0; i < root.Files.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var file = root.Files[i];

                if (file.Reference.PermitsVariant(file.Path.RelativeOrFullPath))
                {
                    var numberMatch = end_number_regex.Match(file.Name);
                    if (numberMatch.Success)
                    {
                        var trimmedName = numberMatch.Groups[1].Value;

                        if (seenVariantBases.Add(trimmedName))
                        {
                            var pathExt = Path.GetExtension(file.Path.RelativeOrFullPath);

                            var variantFile = file with
                            {
                                Name = trimmedName,
                                Path = new AssetPath(
                                    Path.ChangeExtension(file.Path.FullPath, null).TrimEnd(number_chars) + pathExt,
                                    Path.ChangeExtension(file.Path.RelativeOrFullPath, null).TrimEnd(number_chars) + pathExt
                                ),
                                Variants = GetVariantData(root.Files.Select(x => x.Name), trimmedName),
                            };

                            sb.AppendLine($"{indent}    public static class {NormalizeName(variantFile.Name)}");
                            sb.AppendLine($"{indent}    {{");

                            sb.AppendLine(variantFile.Reference.GenerateCode(assemblyName, variantFile, $"{indent}        "));

                            sb.AppendLine($"{indent}    }}");
                            sb.AppendLine();
                        }
                    }
                }

                sb.AppendLine($"{indent}    public static class {NormalizeName(file.Name)}");
                sb.AppendLine($"{indent}    {{");

                sb.AppendLine(file.Reference.GenerateCode(assemblyName, file, $"{indent}        "));

                sb.AppendLine($"{indent}    }}");

                if (i != root.Files.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            if (root.Files.Count > 0 && root.Nodes.Count > 0)
            {
                sb.AppendLine();
            }

            var j = 0;
            foreach (var node in root.Nodes.Values)
            {
                if (j++ != 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(GenerateTextFromPathNode(assemblyName, node, token, depth + 1));
            }

            if (depth != 0)
            {
                sb.AppendLine($"{indent}}}");
            }

            return sb.ToString().TrimEnd();
        }

        static string NormalizeName(string name)
        {
            // - Replace any non-alphanumeric characters with underscores,
            // - trim trailing underscores as a result of variant number slices
            //   or simply poor naming.
            return non_alphanumeric
                  .Replace(name, "_")
                  .TrimEnd('_');
        }
    }

    private static VariantData GetVariantData(IEnumerable<string> fileNames, string assetName)
    {
        var variantMin = int.MaxValue;
        var variantMax = 0;

        foreach (var name in fileNames)
        {
            if (!name.Contains(assetName))
            {
                continue;
            }

            var numberResult = end_number_regex.Match(Path.GetFileNameWithoutExtension(name));
            if (numberResult.Success)
            {
                var num = int.Parse(numberResult.Groups[2].Value);
                variantMin = Math.Min(variantMin, num);
                variantMax = Math.Max(variantMax, num);
            }
        }

        return new VariantData(Start: variantMin, End: variantMax);
    }
}
