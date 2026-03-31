using System.IO;
using Microsoft.Build.Framework;
using Tomat.TML.Build.Common;

namespace Tomat.TML.Build.MSBuild.Tasks;

/// <summary>
///     Runs during <c>dotnet restore</c> (hooked into
///     <c>_GenerateRestoreProjectSpec</c>) to:
///     <list type="bullet">
///         <item>Refresh the version cache if stale.</item>
///         <item>
///             For GitHub versions: download the tML zip if needed, repackage
///             it as a nupkg in the local feed, and write/refresh the alias
///             props file so <c>Sdk.props</c> can resolve aliases at evaluation
///             time.
///         </item>
///         <item>
///             For Steam/dev versions: write/refresh the alias props file so
///             <c>Sdk.targets</c> can import <c>tMLMod.targets</c> directly.
///         </item>
///     </list>
///     After this task completes, NuGet restore finds the nupkg in the local
///     feed and the subsequent <c>dotnet build</c> needs no further version
///     resolution.
/// </summary>
public sealed class TmlRestoreTask : BaseTask
{
    /// <summary>
    ///     The raw version string from <c>$(TmlVersion)</c>.  May be an alias
    ///     ("stable", "preview", "steam", "dev") or a concrete version.
    /// </summary>
    [Required]
    public required string TmlVersion { get; set; }

    /// <summary>
    ///     The cache root directory, matching <c>$(TmlBuildCacheDir)</c> in
    ///     <c>Sdk.props</c>.  Must agree with <c>Platform.GetAppDir</c>.
    /// </summary>
    [Required]
    public required string CacheDir { get; set; }

    /// <summary>
    ///     The local NuGet feed directory, matching <c>$(TmlBuildFeedDir)</c>.
    /// </summary>
    [Required]
    public required string FeedDir { get; set; }

    /// <summary>
    ///     The path to write the alias props file, matching
    ///     <c>$(TmlBuildAliasPropsFile)</c>.
    /// </summary>
    [Required]
    public required string AliasPropsFile { get; set; }

    protected override bool Run()
    {
        Log.LogMessage(
            MessageImportance.Normal,
            "TmlRestoreTask: resolving tML version '{0}'",
            TmlVersion
        );

        // Ensure the feed directory exists so NuGet doesn't reject the source.
        Directory.CreateDirectory(FeedDir);

        var cache = VersionManager.ReadOrCreateVersionCache(CacheDir);

        // Always refresh alias props.  Steam/dev paths may have changed, and
        // the stable/preview pointers may have been updated by the cache
        // refresh.
        VersionManager.WriteAliasProps(cache);

        if (TmlVersion is "steam" or "dev")
        {
            // Nothing more to do: Sdk.targets will import directly.
            Log.LogMessage(
                MessageImportance.Normal,
                "TmlRestoreTask: using local version '{0}', skipping NuGet feed population.",
                TmlVersion
            );
            return true;
        }

        var resolved = VersionManager.ResolveAlias(cache, TmlVersion);
        if (resolved == ModLoaderVersion.Unknown)
        {
            Log.LogError(
                "TmlRestoreTask: could not resolve tML version '{0}'. " +
                "Check that the version exists on GitHub or use 'stable'/'preview'.",
                TmlVersion
            );
            return false;
        }

        if (cache.IsVersionPackaged(resolved))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "TmlRestoreTask: nupkg for tML {0} already in feed, nothing to do.",
                resolved
            );
            return true;
        }

        Log.LogMessage(
            MessageImportance.Normal,
            "TmlRestoreTask: packaging tML {0} into local feed...",
            resolved
        );

        // EnsureVersionPackagedAsync downloads and extracts the zip if needed,
        // then builds the nupkg.  We block here; restore tasks run
        // synchronously.
        cache.EnsureVersionPackagedAsync(resolved).GetAwaiter().GetResult();

        Log.LogMessage(
            MessageImportance.Normal,
            "TmlRestoreTask: tML {0} packaged at '{1}'.",
            resolved,
            cache.GetNupkgPath(resolved)
        );
        return true;
    }
}
