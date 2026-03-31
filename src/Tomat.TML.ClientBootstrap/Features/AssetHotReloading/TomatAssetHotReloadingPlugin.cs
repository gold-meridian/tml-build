using log4net;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

public sealed class TomatAssetHotReloadingPlugin : LaunchPlugin
{
    private const string id = "tomat.assethotreloading";

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override LaunchPluginMetadata Metadata { get; } = new(
        UniqueId: id,
        DisplayName: "Asset Hot Reloading",
        Version: "1.0.0",
        Authors: "tomat",
        Description: "Listens for changes to files in loaded mod sources and hot reloads assets in-game. Capable of compiling shaders.",
        IconProvider: () => null
    );

    public override void ApplyPatches(LaunchContext ctx)
    {
        base.ApplyPatches(ctx);

        if (ctx.LaunchMode != LaunchMode.Client)
        {
            return;
        }

        HotAssetSystem.Initialize(in ctx);
    }

    public override void LoadContent(LaunchContext ctx)
    {
        base.LoadContent(ctx);

        if (ctx.LaunchMode != LaunchMode.Client)
        {
            return;
        }

        HotAssetSystem.LoadContent(in ctx);
    }
}
