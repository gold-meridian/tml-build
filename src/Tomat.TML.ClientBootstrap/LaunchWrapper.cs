using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap;

internal static class LaunchWrapper
{
    private static Task? hookTask;

    private static readonly HookManager logging_hooks = new();
    private static readonly HookManager startup_hooks = new();
    public static ILog Logger { get; } = LogManager.GetLogger("Tomat.TML.ClientBootstrap");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PatchAndRun(LaunchContext ctx)
    {
        var args = (List<string>)["-console", ..ctx.GameLaunchArguments];
        if (ctx.LaunchMode is LaunchMode.Server)
        {
            args.Add("-server");
        }

        InitializeLogging(
            ctx.LaunchMode is LaunchMode.Client ? Logging.LogFile.Client : Logging.LogFile.Server,
            args.Prepend(Path.Combine(ctx.GameDirectory, ctx.GameBinaryName)).ToArray()
        );

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

        Logger.Info("Running plugin on-loads...");

        foreach (var plugin in plugins)
        {
            plugin.Load(ctx);
        }

        Logger.Info("Finished running plugin on-loads!");

        hookTask = Task.Run(
            () =>
            {
                var times = new Dictionary<string, Stopwatch>();
                var totalTime = Stopwatch.StartNew();

                Logger.Info("Patching early-load wait...");

                startup_hooks.Add(
                    typeof(Main).GetMethod("LoadContent", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!,
                    (Action<Main> orig, Main self) =>
                    {
                        Logger.Info("Running plugin on-LoadContent hooks...");

                        foreach (var plugin in plugins)
                        {
                            plugin.LoadContent(ctx);
                        }

                        Logger.Info("Finished running plugin on-LoadContent hooks...");

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
                );

                startup_hooks.Apply(inParallel: false);

                Logger.Info("Finished patching game!");

                foreach (var feature in plugins)
                {
                    times[feature.UniqueId] = Stopwatch.StartNew();
                    {
                        feature.ApplyPatches(ctx);
                    }
                    times[feature.UniqueId].Stop();
                }

                ctx.Hooks.Apply(inParallel: true);

                totalTime.Stop();

                Logger.Info($"Total patch time: {totalTime.Elapsed:g}");
                Logger.Info("Per-feature patch queue time (not patch applications):");
                foreach (var feature in plugins)
                {
                    Logger.Info($"    {feature.UniqueId}: {times[feature.UniqueId].Elapsed:g}");
                }
            }
        );

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
    private static void InitializeLogging(Logging.LogFile logFile, string[] launchArgs)
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
            logging_hooks.Modify(
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
            );

            // Patch the visible arguments to reflect what is actually given to
            // the game.
            logging_hooks.Add(
                typeof(Environment).GetMethod(nameof(Environment.GetCommandLineArgs), BindingFlags.Public | BindingFlags.Static)!,
                () => launchArgs
            );

            logging_hooks.Apply(inParallel: false);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to patch log initialization, this is bad!\n{e}");
        }

        Logger.Info("Successfully patched!");
    }
}
