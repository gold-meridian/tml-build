using log4net;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

public sealed class TomatEnableModPlugin : LaunchPlugin
{
    private const string id = "tomat.enablemod";

    public override string UniqueId => id;

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override void LoadContent(LaunchContext ctx)
    {
        base.LoadContent(ctx);

        if (ctx.RequestedModName is null)
        {
            return;
        }

        logger.Info($"Ensuring mod is enabled: {ctx.RequestedModName}");
        ModLoader.EnableMod(ctx.RequestedModName);
    }
}
