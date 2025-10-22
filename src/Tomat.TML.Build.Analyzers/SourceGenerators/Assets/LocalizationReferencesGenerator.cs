using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Hjson;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Tomat.TML.Build.Analyzers.SourceGenerators.Assets;

/// <summary>
///     Generates strongly-typed references to localization keys.
/// </summary>
[Generator]
public sealed class LocalizationReferencesGenerator : IIncrementalGenerator
{
    private static readonly Regex arg_remapping_regex = new(@"(?<={\^?)(\d+)(?=(?::[^\r\n]+?)?})", RegexOptions.Compiled);

    public sealed record LocalizationNode(
        string Name,
        Dictionary<string, LocalizationNode> Nodes,
        List<(string key, string value)> Keys
    );

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = GeneratorsHelper.GetRootNamespaceOrAssemblyName(
            context.AnalyzerConfigOptionsProvider,
            context.CompilationProvider
        );
        var hjsonFilesProvider = context.AdditionalTextsProvider.Where(text => text.Path.EndsWith(".hjson")).Collect();

        context.RegisterSourceOutput(
            hjsonFilesProvider.Combine(rootNamespaceProvider),
            (ctx, tuple) =>
            {
                var (additionalTexts, rootNamespace) = tuple;
                ctx.AddSource(
                    "LocalizationReferences.g.cs",
                    GenerateLocalization(additionalTexts, rootNamespace, ctx.CancellationToken)
                );
            }
        );
    }

    private static string GenerateLocalization(ImmutableArray<AdditionalText> texts, string rootNamespace, CancellationToken token)
    {
        var keys = new HashSet<(string key, string value)>();

        foreach (var text in texts)
        {
            // Validated in a compiler pre-build step.
            /*
            if (!HjsonValidator.ValidateHjsonFile(text.FullPath))
            {
                continue;
            }
            */

            token.ThrowIfCancellationRequested();

            foreach (var key in GetKeysFromFile(text, token))
            {
                keys.Add(key);
            }
        }

        var root = new LocalizationNode("", [], []);

        foreach (var key in keys)
        {
            var parts = key.key.Split('.');
            var current = root;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i == parts.Length - 1)
                {
                    current.Keys.Add(key);
                }
                else
                {
                    if (!current.Nodes.TryGetValue(part, out var node))
                    {
                        node = new LocalizationNode(part, [], []);
                        current.Nodes.Add(part, node);
                    }

                    current = node;
                }
            }
        }

        var sb = new StringBuilder();

        foreach (var node in root.Nodes.Values)
        {
            token.ThrowIfCancellationRequested();

            sb.Append(GenerateTextFromLocalizationNode(node, "", 1));
        }

        return
            $$"""
              #nullable enable

              using Terraria.Localization;

              namespace {{rootNamespace}}.Core;

              // ReSharper disable MemberHidesStaticFromOuterClass
              internal static class LocalizationReferences
              {
              {{sb.ToString().TrimEnd()}}
              }
              """;
    }

    private static string GenerateTextFromLocalizationNode(LocalizationNode node, string parentKey, int depth = 0)
    {
        var ourKey = (parentKey + '.' + node.Name).TrimStart('.');

        var sb = new StringBuilder();
        var indent = new string(' ', depth * 4);

        if (depth > 1)
        {
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}public static class {node.Name}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    public const string KEY = \"{ourKey}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}    public static LocalizedText GetChildText(string childKey)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        return Language.GetText(KEY + '.' + childKey);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    public static string GetChildTextValue(string childKey, params object?[] values)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        return Language.GetTextValue(KEY + '.' + childKey, values);");
        sb.AppendLine($"{indent}    }}");

        for (var i = 0; i < node.Keys.Count; i++)
        {
            sb.AppendLine();

            var (key, value) = node.Keys[i];
            var name = key.Split('.').Last();
            var args = GetArgumentCount(value);

            sb.AppendLine($"{indent}    public static class {name}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        public const string KEY = \"{key}\";");
            sb.AppendLine($"{indent}        public const int ARG_COUNT = {args};");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public static LocalizedText GetText()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return Language.GetText(KEY);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();

            if (args == 0)
            {
                sb.AppendLine($"{indent}        public static string GetTextValue()");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            return Language.GetTextValue(KEY);");
                sb.AppendLine($"{indent}        }}");
            }
            else
            {
                var argNames = new List<string>();
                for (var j = 0; j < args; j++)
                {
                    argNames.Add($"arg{j}");
                }

                sb.AppendLine($"{indent}        public static string GetTextValue({string.Join(", ", argNames.Select(x => $"object? {x}"))})");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            return Language.GetTextValue(KEY, {string.Join(", ", argNames)});");
                sb.AppendLine($"{indent}        }}");
            }

            sb.AppendLine();
            sb.AppendLine($"{indent}        public static LocalizedText GetChildText(string childKey)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return Language.GetText(KEY + '.' + childKey);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public static string GetChildTextValue(string childKey, params object?[] values)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return Language.GetTextValue(KEY + '.' + childKey, values);");
            sb.AppendLine($"{indent}        }}");

            sb.AppendLine($"{indent}    }}");
        }

        foreach (var child in node.Nodes.Values)
        {
            sb.Append(GenerateTextFromLocalizationNode(child, ourKey, depth + 1));
        }

        sb.AppendLine($"{indent}}}");

        return sb.ToString();
    }

    private static List<(string key, string value)> GetKeysFromFile(AdditionalText file, CancellationToken token)
    {
        var keys = new List<(string key, string value)>();
        var prefix = GetPrefixFromPath(file.Path);
        var text = file.GetText(token)?.ToString() ?? throw new InvalidOperationException($"Failed to read HJSON file: {file.Path}");
        var json = HjsonValue.Parse(text).ToString();
        var jsonObject = JObject.Parse(json);

        foreach (var t in jsonObject.SelectTokens("$..*"))
        {
            if (t.HasValues)
            {
                continue;
            }

            if (t is JObject { Count: 0 })
            {
                continue;
            }

            var path = "";
            var current = t;

            for (var parent = t.Parent; parent is not null; parent = parent.Parent)
            {
                path = parent switch
                {
                    JProperty property => property.Name + (path == string.Empty ? string.Empty : '.' + path),
                    JArray array => array.IndexOf(current) + (path == string.Empty ? string.Empty : '.' + path),
                    _ => path,
                };
                current = parent;
            }

            path = path.Replace(".$parentVal", "");
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                path = prefix + '.' + path;
            }

            var value = t.Type switch
            {
                JTokenType.String => t.Value<string>() ?? "",
                JTokenType.Integer => t.Value<int>().ToString(),
                JTokenType.Boolean => t.Value<bool>().ToString(),
                JTokenType.Float => t.Value<float>().ToString(CultureInfo.InvariantCulture),
                _ => t.ToString(),
            };

            keys.Add((path, value));
        }

        return keys;
    }

    private static string? GetPrefixFromPath(string path)
    {
        path = Path.GetFileNameWithoutExtension(path);
        var splitByUnderscore = path.Split('_');

        return splitByUnderscore.Length == 2 ? splitByUnderscore[1] : null;
    }

    private static int GetArgumentCount(string value)
    {
        return arg_remapping_regex.Matches(value).Count;
    }
}
