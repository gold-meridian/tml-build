using System;
using System.IO;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

internal abstract class HotAssetReloader
{
    public abstract bool QueuePath(string path);

    public abstract void OnWatcherUpdate(HotReloadContext mod, string path);
}

internal class ShaderSourceHotReloader : HotAssetReloader
{
    public override bool QueuePath(string path) => false;

    public override void OnWatcherUpdate(HotReloadContext mod, string path)
    {
        var fullPath = HotAssetSystem.NormalizePath(Path.Combine(mod.SourceFolder, path));

        var logger = new GameLogWrapper(HotAssetSystem.Logger);
        try
        {
            var compiled = Shaderc.Compile(in logger, HotAssetSystem.NativesDir, fullPath);
            if (!compiled)
            {
                throw new Exception("shaderc reported error diagnostics");
            }
        }
        catch (Exception e)
        {
            logger.Warn("Shader compilation failed with exception: " + e.Message);
        }
    }
}
