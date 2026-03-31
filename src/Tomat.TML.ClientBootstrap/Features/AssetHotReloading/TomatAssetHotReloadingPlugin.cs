using log4net;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

public sealed class TomatAssetHotReloadingPlugin : LaunchPlugin
{
    private const string id = "tomat.assethotreloading";

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override string UniqueId => id;

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
