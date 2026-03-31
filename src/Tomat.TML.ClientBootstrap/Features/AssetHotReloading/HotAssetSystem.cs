using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using ReLogic.Content;
using ReLogic.Content.Readers;
using ReLogic.Utilities;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Assets;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

internal sealed record HotReloadContext(Mod Mod) : IDisposable
{
    public string SourceFolder => Mod.SourceFolder;

    public FileSystemWatcher Watcher { get; } = new(Mod.SourceFolder)
    {
        NotifyFilter = NotifyFilters.Attributes | NotifyFilters.Security | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite,
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
    };

    public HotAssetContentSource Source { get; } = new(Mod.SourceFolder);

    public bool NeedsReload { get; set; }

    public void Dispose()
    {
        Watcher.Dispose();
    }
}

internal static class HotAssetSystem
{
    private static readonly Dictionary<string, HotAssetReloader> reloader_by_extension = new()
    {
        { ".fx", new ShaderSourceHotReloader() },
        { ".hlsl", new ShaderSourceHotReloader() },
    };

    public static string? NativesDir { get; private set; }

    public static ILog Logger { get; private set; } = LogManager.GetLogger("AssetHotReload");

    public static bool IsReloadingAssetsRightNow { get; private set; }

    private static readonly Dictionary<string, HotReloadContext> mods = [];
    private static string[] supportedExtensions = [];

    private static CancellationTokenSource? tokenSource = null;
    private static readonly object token_lock = new();

    public static void Initialize(in LaunchContext ctx)
    {
        NativesDir = ctx.SdkNativesDirectory;

        ctx.Hooks.Add(
            typeof(PngReader).GetMethod("PreMultiplyAlpha", BindingFlags.NonPublic | BindingFlags.Static)!,
            PreMultiplyAlpha_SkipIfLoadingFromMod
        );

        ctx.Hooks.Add(
            typeof(TModContentSource).GetMethod(nameof(TModContentSource.OpenStream), BindingFlags.Public | BindingFlags.Instance)!,
            OpenStream_UseHotReloadContext
        );

        ctx.Hooks.Add(
            typeof(ModContent).GetMethod(nameof(ModContent.Load), BindingFlags.NonPublic | BindingFlags.Static)!,
            Load_HandleModListeners
        );

        ctx.Hooks.Add(
            typeof(ModContent).GetMethod(nameof(ModContent.Unload), BindingFlags.NonPublic | BindingFlags.Static)!,
            Unload_DisposeModListeners
        );

        return;

        static void PreMultiplyAlpha_SkipIfLoadingFromMod(Action<nint, int> orig, nint img, int len)
        {
            if (IsReloadingAssetsRightNow)
            {
                return;
            }

            orig(img, len);
        }

        static Stream OpenStream_UseHotReloadContext(Func<TModContentSource, string, Stream> orig, TModContentSource self, string assetName)
        {
            lock (mods)
            {
                if (mods.TryGetValue(self.file.Name, out var ctx) && ctx.Source.EnumerateAssets().Contains(assetName))
                {
                    return ctx.Source.OpenStream(assetName);
                }

                return orig(self, assetName);
            }
        }

        static void Load_HandleModListeners(Action<CancellationToken> orig, CancellationToken token)
        {
            orig(token);

            try
            {
                lock (mods)
                {
                    if (mods.Count != 0)
                    {
                        throw new Exception("Mod listeners aren't cleared?!");
                    }

                    foreach (var mod in ModLoader.Mods.Where(x => !string.IsNullOrEmpty(x.SourceFolder) && Directory.Exists(x.SourceFolder)))
                    {
                        mods[mod.Name] = new HotReloadContext(mod);
                    }

                    foreach (var mod in mods.Values)
                    {
                        mod.Watcher.Changed += OnFileChanged(mod);
                        mod.Watcher.Created += OnFileCreated(mod);
                        mod.Watcher.Deleted += OnFileDeleted(mod);
                        mod.Watcher.Renamed += OnFileRenamed(mod);
                    }

                    supportedExtensions = Main.instance.Services.Get<AssetReaderCollection>().GetSupportedExtensions();

                    SpinUpThread();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to load mod listeners: {e}");
            }
        }

        static void Unload_DisposeModListeners(Action orig)
        {
            try
            {
                lock (mods)
                {
                    foreach (var mod in mods.Values)
                    {
                        mod.Dispose();
                    }

                    mods.Clear();
                }

                lock (token_lock)
                {
                    tokenSource?.Cancel();
                    tokenSource?.Dispose();
                    tokenSource = null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to dispose mod listeners: {e}");
            }

            orig();
        }
    }

    public static void LoadContent(in LaunchContext ctx)
    {
        SpinUpThread();
    }

    private static void SpinUpThread()
    {
        lock (token_lock)
        {
            if (tokenSource is not null)
            {
                return;
            }

            tokenSource = new CancellationTokenSource();
            Task.Run(UpdateAssetReloader, tokenSource.Token);
        }

        return;

        static async Task UpdateAssetReloader()
        {
            try
            {
                lock (token_lock)
                {
                    if (tokenSource is null)
                    {
                        return;
                    }

                    tokenSource.Token.ThrowIfCancellationRequested();
                }

                while (true)
                {
                    lock (token_lock)
                    {
                        if (tokenSource is null)
                        {
                            return;
                        }

                        tokenSource.Token.ThrowIfCancellationRequested();
                    }

                    lock (mods)
                    {
                        foreach (var mod in mods.Values.Where(n => n.NeedsReload))
                        {
                            ReloadModAssets(mod);
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            finally
            {
                lock (token_lock)
                {
                    tokenSource = null;
                }
            }

            // ReSharper disable once FunctionNeverReturns
        }
    }

    private static void ReloadModAssets(HotReloadContext mod)
    {
        // Forcefully reload assets.
        Main.RunOnMainThread(
            () =>
            {
                IsReloadingAssetsRightNow = true;

                try
                {
                    mod.NeedsReload = false;

                    // First set to only the normal content source to unapply any
                    // previous hot reload changes, then apply with the hot reload
                    // source to re-apply changes.
                    // We can't just re-apply with the hot reload source already
                    // present because if a modified file is modified again, it
                    // won't be replicated in-game (without first unapplying).
                    mod.Mod.Assets.SetSources([mod.Mod.RootContentSource]);
                    mod.Mod.Assets.SetSources([mod.Source, mod.Mod.RootContentSource]);
                }
                finally
                {
                    IsReloadingAssetsRightNow = false;
                }
            }
        );
    }

    private static FileSystemEventHandler OnFileChanged(HotReloadContext mod)
    {
        return (_, args) =>
        {
            var relativePath = NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
            {
                return;
            }

            if (CommonSkipQueue(mod, relativePath))
            {
                return;
            }

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static FileSystemEventHandler OnFileCreated(HotReloadContext mod)
    {
        return (_, args) =>
        {
            var relativePath = NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
            {
                return;
            }

            if (CommonSkipQueue(mod, relativePath))
            {
                return;
            }

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static FileSystemEventHandler OnFileDeleted(HotReloadContext mod)
    {
        return (_, args) =>
        {
            // Maybe ignore deleting entirely?

            var relativePath = NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
            {
                return;
            }

            if (CommonSkipQueue(mod, relativePath))
            {
                return;
            }

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static RenamedEventHandler OnFileRenamed(HotReloadContext mod)
    {
        return (_, args) =>
        {
            var relativeOldPath = NormalizePath(args.OldFullPath[mod.SourceFolder.Length..]);
            var relativePath = NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath) || IgnoreCompletely(relativeOldPath))
            {
                return; // TODO
            }

            if (CommonSkipQueue(mod, relativePath))
            {
                return;
            }

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativeOldPath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
        };
    }

    private static bool CommonSkipQueue(HotReloadContext mod, string relativePath)
    {
        // Handle special cases
        var extension = Path.GetExtension(relativePath);
        if (reloader_by_extension.TryGetValue(extension, out var reloader))
        {
            reloader.OnWatcherUpdate(mod, relativePath);

            if (!reloader.QueuePath(relativePath))
            {
                return true;
            }
        }

        if (!supportedExtensions.Contains(extension))
        {
            return true;
        }

        mod.NeedsReload = true;
        return false;
    }

    private static bool IgnoreCompletely(string path)
    {
        // ModCompile.IgnoreCompletely
        return path[0] == '.' || path.StartsWith("bin/") || path.StartsWith("obj/");
    }

    public static string NormalizePath(string path)
    {
        path = path.StartsWith('\\') || path.StartsWith('/') ? path[1..] : path;
        return path.Replace('\\', '/');
    }
}
