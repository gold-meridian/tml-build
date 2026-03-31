using log4net;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

public sealed class TomatEnableModPlugin : LaunchPlugin
{
    private const string id = "tomat.enablemod";

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override LaunchPluginMetadata Metadata { get; } = new(
        UniqueId: id,
        DisplayName: "Force Enable Mod",
        Version: "1.0.0",
        Authors: "tomat",
        Description: "Ensures the mod you're debugging is always enabled on launch, even if it got disabled and wasn't rebuilt.",
        IconProvider: () => null
    );

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
