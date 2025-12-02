using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tomat.TML.Build.Analyzers;

/// <summary>
///     Utilities for source generation.
/// </summary>
public static class GeneratorsHelper
{
    /// <summary>
    ///     Gets the root namespace of a project, or the assembly name if the
    ///     root namespace cannot be resolved.
    /// </summary>
    public static IncrementalValueProvider<string> GetRootNamespaceOrAssemblyName(
        IncrementalValueProvider<AnalyzerConfigOptionsProvider> configProvider,
        IncrementalValueProvider<Compilation> compilationProvider
    )
    {
        return configProvider
              .Select(GetRootNamespace)
              .Combine(compilationProvider)
              .Select(GetAssemblyNameFallback);

        static string? GetRootNamespace(
            AnalyzerConfigOptionsProvider config,
            CancellationToken _
        )
        {
            return config.GlobalOptions.TryGetValue("build_property.RootNamespace", out var @namespace) ? @namespace : null;
        }

        static string GetAssemblyNameFallback(
            (string? Left, Compilation Right) tuple,
            CancellationToken _
        )
        {
            var (rootNamespace, compilation) = tuple;
            return rootNamespace ?? compilation.AssemblyName ?? throw new InvalidOperationException("Could not get root namespace (or AssemblyName fallback) for compilation");
        }
    }
}
