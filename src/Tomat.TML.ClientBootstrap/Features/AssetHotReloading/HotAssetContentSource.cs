using System;
using System.Collections.Generic;
using System.IO;
using ReLogic.Content;
using ReLogic.Content.Sources;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

internal sealed class HotAssetContentSource : ContentSource
{
    public string[] AssetPaths
    {
        get => assetPaths;
        set => assetPaths = value;
    }

    private readonly string directory;

    /// <summary>
    ///     Initializes the content source to read from the base directory
    ///     <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The base directory to read from.</param>
    public HotAssetContentSource(string directory)
    {
        this.directory = directory;
        assetPaths = [];
    }

    /// <summary>
    ///     Refreshes asset names given the current value of
    ///     <see cref="AssetPaths"/>.
    /// </summary>
    public void RefreshAssetNames()
    {
        try
        {
            SetAssetNames(AssetPaths);
        }
        catch (Exception ex)
        {
            new GameLogWrapper(HotAssetSystem.Logger).Warn("Failed to refresh asset names: " + ex);
        }
    }

    /// <summary>
    ///     Opens a stream to a file directly on disk.
    /// </summary>
    public override Stream OpenStream(string fullAssetName)
    {
        return File.OpenRead(Path.Combine(directory, fullAssetName));
    }

#if NET10_0_OR_GREATER
    public override string? FileWatcherPath => null;

    public override void Refresh()
    {
        RefreshAssetNames();
    }
#endif
}
