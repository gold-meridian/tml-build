using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Features;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap;

internal static class LaunchWrapper
{
    // ReSharper disable once CollectionNeverQueried.Local
    private static readonly List<IDisposable> hooks = [];

    private static Task? hookTask;

    public static ILog Logger { get; } = LogManager.GetLogger("Tomat.TML.ClientBootstrap");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PatchAndRun(LaunchContext ctx)
    {
        InitializeLogging(ctx.LaunchMode is LaunchMode.Client ? Logging.LogFile.Client : Logging.LogFile.Server);

        Logger.Info("This tModLoader instance is being launched using Tomat.TML.ClientBootstrap.");
        Logger.Info($"The following features are enabled: {string.Join(", ", ctx.RequestedFeatures)}");
        Logger.Info($"Mode: {ctx.LaunchMode}, mod name: {ctx.RequestedModName ?? "<null>"}");
        Logger.Info("Please be aware any issues encountered with tModLoader may be a resulted of these modifications.");

        Logger.Info("Initializing plugins...");
        var pluginRepo = new PluginRepository();
        {
            pluginRepo.AddPluginsFromAssembly(typeof(Program).Assembly);
        }
        Logger.Info("Initialized plugins!");

        var plugins = new List<LaunchPlugin>();
        foreach (var feature in ctx.RequestedFeatures)
        {
            Logger.Info($"Processing requested feature: {feature}");

            if (pluginRepo.TryGetPlugin(feature, out var plugin))
            {
                Logger.Info($"    Found plugin of same name: {plugin.UniqueId}");
                plugins.Add(plugin);
            }
            else
            {
                // TODO: Consider an error?  This probably shouldn't prevent a
                //       game launch.
                Logger.Warn($"    Could not found matching plugin with same name: {feature}");
            }
        }

        Logger.Info("Finished resolving plugins!");

        hookTask = Task.Run(
            () =>
            {
                var times = new Dictionary<string, Stopwatch>();
                var totalTime = Stopwatch.StartNew();

                Logger.Info("Patching early-load wait...");

                hooks.Add(
                    new Hook(
                        typeof(Main).GetMethod("LoadContent", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!,
                        static (Action<Main> orig, Main self) =>
                        {
                            if (hookTask is null)
                            {
                                Logger.Error("LoadContent: Failed to await for hooks to finish applying, hookTask is null?");
                                throw new InvalidOperationException("Build wrapper failed to apply hooks? No task found.");
                            }

                            if (!hookTask.IsCompleted)
                            {
                                Logger.Info("LoadContent: Reached loading point before hooks finished, waiting for completion...");
                                hookTask.ConfigureAwait(false).GetAwaiter().GetResult();
                            }

                            orig(self);
                        }
                    )
                );

                Logger.Info("Finished patching game!");

                foreach (var feature in plugins)
                {
                    times[feature.UniqueId] = Stopwatch.StartNew();
                    {
                        feature.ApplyPatches(ctx);
                    }
                    times[feature.UniqueId].Stop();
                }

                totalTime.Stop();

                Logger.Info($"Total patch time: {totalTime.Elapsed:g}");
                Logger.Info("Per-feature patch time:");
                foreach (var feature in plugins)
                {
                    Logger.Info($"    {feature.UniqueId}: {times[feature.UniqueId].Elapsed:g}");
                }
            }
        );

        // TODO: Support pass-through arguments.
        var args = new List<string> { "-console" };
        if (ctx.LaunchMode is LaunchMode.Server)
        {
            args.Add("-server");
        }

        if (typeof(ModLoader).Assembly.EntryPoint is not { } entryPoint)
        {
            throw new InvalidOperationException("Cannot launch tML, no entrypoint found!");
        }

        entryPoint.Invoke(null, [args.ToArray()]);
    }

    /// <summary>
    ///     Early-initializes tModLoader's logging routine and then stubs it to
    ///     not do anything during normal execution.
    /// </summary>
    private static void InitializeLogging(Logging.LogFile logFile)
    {
        Utils.TryCreatingDirectory(Logging.LogDir);

        try
        {
            Logging.InitLogPaths(logFile);
            Logging.ConfigureAppenders(logFile);
            Logging.TryUpdatingFileCreationDate(Logging.LogPath);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to early-load logging: {e}");
        }

        Logger.Info("Early-load logging initialization complete, this should be the first message you see.");
        Logger.Info("Attempting to patch the existing logging initialization routine...");

        try
        {
            hooks.Add(
                new ILHook(
                    typeof(Logging).GetMethod(nameof(Logging.Init), BindingFlags.NonPublic | BindingFlags.Static)!,
                    il =>
                    {
                        var c = new ILCursor(il);

                        // Assume first branch is to exit the function early.
                        // We just let it run the routines before
                        // initialization, since we initialize the logger
                        // ourselves.
                        c.GotoNext(MoveType.Before, x => x.MatchBrfalse(out _));

                        c.EmitPop();
                        c.EmitLdcI4(1);
                    }
                )
            );
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to patch log initialization, this is bad!\n{e}");
        }

        Logger.Info("Successfully patched!");
    }
}
