using System;
using System.Reflection;
using log4net;
using MonoMod.RuntimeDetour;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

public sealed class TomatEnableModPlugin : LaunchPlugin
{
    private const string id = "tomat.enablemod";

    public override string UniqueId => id;

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override void Load(LaunchContext ctx)
    {
        base.Load(ctx);

        if (ctx.RequestedModName is null)
        {
            return;
        }

        ObjectHolder.Add(
            new Hook(
                typeof(Terraria.Program).GetMethod(nameof(Terraria.Program.RunGame), BindingFlags.Public | BindingFlags.Static)!,
                (Action orig) =>
                {
                    logger.Info($"Ensuring mod is enabled: {ctx.RequestedModName}");
                    ModLoader.EnableMod(ctx.RequestedModName);
                    orig();
                }
            )
        );
    }
}
