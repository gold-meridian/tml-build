using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Tomat.TML.ClientBootstrap.Features;

namespace Tomat.TML.ClientBootstrap;

internal static class LaunchWrapper
{
    // ReSharper disable once CollectionNeverQueried.Local
    private static readonly List<IDisposable> hooks = [];

    private static Task? hookTask;

    public static ILog Logger { get; } = LogManager.GetLogger("Tomat.TML.ClientBootstrap");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PatchAndRun(string mode, string modName, string[] enabledFeatures)
    {
        Logger.Info("This tModLoader instance is being launched using Tomat.TML.ClientBootstrap.");
        Logger.Info($"The following features are enabled: {string.Join(", ", enabledFeatures)}");
        Logger.Info($"Mode: {mode}, mod name: {modName}");
        Logger.Info("Please be aware any issues encountered with tModLoader may be a resulted of these modifications.");

        var knownFeatures = new Dictionary<string, AssemblyFeature>
        {
            { "ppeb.netcoredbg", new PpebNetCoreDbgFeature() },
            { "tomat.enablemod", new TomatEnableModFeature(modName) },
        };

        var features = knownFeatures
                      .Where(x => enabledFeatures.Contains(x.Key))
                      .ToArray();

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

                foreach (var (name, feature) in features)
                {
                    times[name] = Stopwatch.StartNew();
                    {
                        feature.Apply();
                    }
                    times[name].Stop();
                }

                totalTime.Stop();

                Logger.Info($"Total patch time: {totalTime.Elapsed:g}");
                Logger.Info("Per-feature patch time:");
                foreach (var (name, _) in features)
                {
                    Logger.Info($"    {name}: {times[name].Elapsed:g}");
                }
            }
        );

        // TODO: Support pass-through arguments.
        var args = new List<string> { "-console" };
        if (mode == "server")
        {
            args.Add("-server");
        }

        if (typeof(ModLoader).Assembly.EntryPoint is not { } entryPoint)
        {
            throw new InvalidOperationException("Cannot launch tML, no entrypoint found!");
        }

        entryPoint.Invoke(null, [args.ToArray()]);
    }
}
