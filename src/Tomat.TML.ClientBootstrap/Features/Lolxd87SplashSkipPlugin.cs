using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

// Adapted from:
// https://github.com/EtherealCrusaders/tml-debug-quickstart

public sealed class Lolxd87SplashSkipPlugin : LaunchPlugin
{
    private const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const string id = "lolxd87.splashskip";

    public override string UniqueId => id;

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override void ApplyPatches(LaunchContext ctx)
    {
        base.ApplyPatches(ctx);

        // Stub these out as no-ops.
        
        // Not sure why this one was included in the original program.  Doesn't
        // work properly with filtering?
        /*
        ctx.Hooks.Add(
            typeof(AssemblyManager).GetMethod(nameof(AssemblyManager.IsLoadable), flags)!,
            (AssemblyManager.ModLoadContext _, Type _) => true
        );
        */

        ctx.Hooks.Add(
            typeof(AssemblyManager).GetMethod(nameof(AssemblyManager.JITAssemblies), flags)!,
            (IEnumerable<Assembly> _, PreJITFilter _) => { }
        );

        ctx.Hooks.Add(
            typeof(Terraria.Program).GetMethod(nameof(Terraria.Program.ForceJITOnAssembly), flags)!,
            (IEnumerable<Type> _) => { }
        );

        ctx.Hooks.Add(
            typeof(Terraria.Program).GetMethod(nameof(Terraria.Program.ForceStaticInitializers), flags, [typeof(Assembly)])!,
            (Assembly _) => { }
        );

        // Hasten splash screen.
        ctx.Hooks.Add(
            typeof(Main).GetMethod(nameof(Main.DrawSplash), flags)!,
            (Action<Main, GameTime> orig, Main self, GameTime gameTime) =>
            {
                logger.Info("Entering splash, attempting fast start...");

                var sw = Stopwatch.StartNew();

                for (var i = 0; i < 900 && Main.showSplash; i++)
                {
                    orig(self, gameTime);
                    Main.Assets.TransferCompletedAssets();
                }

                sw.Stop();

                logger.Info($"Finished splash, elapsed time: {sw.Elapsed:g}");
            }
        );
    }
}
