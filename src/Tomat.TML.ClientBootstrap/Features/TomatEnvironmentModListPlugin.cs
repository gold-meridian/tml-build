using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoMod.Cil;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

public sealed class TomatEnvironmentModListPlugin : LaunchPlugin
{
    public override LaunchPluginMetadata Metadata { get; } = new(
        UniqueId: "tomat.environmentmodlist",
        DisplayName: "[tml-build] Environment Mod List",
        Version: "1.0.0",
        Authors: "tomat",
        Description: "Displays information about your tModLoader and tml-build environment in the tmodLoader Mods List.",
        IconProvider: () => null
    );

    private static readonly List<LaunchPluginMetadata> mods = [];

    private static readonly Dictionary<string, LocalMod> local_mods = [];

    public override void Load(LaunchContext ctx, List<LaunchPlugin> plugins)
    {
        base.Load(ctx, plugins);

        mods.Add(MakeTmlMetadata(in ctx));
        mods.Add(MakeTmlBuildMetadata(in ctx));
        mods.AddRange(plugins.Select(x => x.Metadata));

        return;

        static LaunchPluginMetadata MakeTmlMetadata(in LaunchContext ctx)
        {
            var alias = ctx.TmlVersion.ToLowerInvariant();
            var packageType = alias switch
            {
                "stable" or "preview" => "package-provided",
                "steam" or "dev" => "local",

                // Assume a raw version number.
                _ => "package-provided, explicit",
            };

            return new LaunchPluginMetadata(
                UniqueId: "tml",
                DisplayName: $"[tml-build] tModLoader ({ctx.TmlVersion}; {packageType})",
                Version: ctx.TmlVersionResolved,
                Authors: "The TML Team",
                Description: "Information about the tModLoader environment tml-build is using.",
                IconProvider: () => null
            );
        }

        static LaunchPluginMetadata MakeTmlBuildMetadata(in LaunchContext ctx)
        {
            return new LaunchPluginMetadata(
                UniqueId: "tml-build",
                DisplayName: "[tml-build] Tomat.Terraria.ModLoader.Sdk",
                Version: ctx.TmlBuildVersion,
                Authors: "tomat",
                Description: "tml-build is an MSBuild SDK and accompanying projects forming a feature-complete tModLoader development SDK/toolchain.",
                IconProvider: () => null
            );
        }
    }

    public override void ApplyPatches(LaunchContext ctx)
    {
        base.ApplyPatches(ctx);

        ctx.Hooks.Modify(
            typeof(UIMods).GetMethod(nameof(UIMods.Update), BindingFlags.Public | BindingFlags.Instance)!,
            Update_PopulateResult
        );

        return;

        static void Update_PopulateResult(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchCallvirt<Task<List<UIModItem>>>($"get_{nameof(Task<>.Result)}"));
            c.EmitDelegate(InsertOurMods);
        }
    }

    public override void LoadContent(LaunchContext ctx)
    {
        base.LoadContent(ctx);
    }

    private static List<UIModItem> InsertOurMods(List<UIModItem> modItems)
    {
        foreach (var mod in mods)
        {
            modItems.Insert(0, MakeModItem(in mod));
        }

        return modItems;
    }

    private static UIModItem MakeModItem(in LaunchPluginMetadata meta)
    {
        if (!local_mods.TryGetValue(meta.UniqueId, out var localMod))
        {
            local_mods[meta.UniqueId] = localMod = MakeLocalMod(in meta);
        }

        return new UIModItem(localMod);
    }

    private static LocalMod MakeLocalMod(in LaunchPluginMetadata meta)
    {
        var version = Version.TryParse(meta.Version, out var v) ? v : new Version(1, 0, 0);
        var properties = new BuildProperties
        {
            author = meta.Authors,
            version = version,
            displayName = meta.DisplayName,
            description = meta.Description,
            side = ModSide.Client,
        };

        var file = new TmodFile($"tml-build://{meta.UniqueId}.tmod", meta.UniqueId, version);
        {
            if (meta.IconProvider() is { } iconStream)
            {
                using (var ms = new MemoryStream())
                {
                    iconStream.CopyTo(ms);
                    AddCachedFile(file, "icon.png", ms.ToArray());
                }

                AddCachedFile(file, "Info", properties.ToBytes());
                AddCachedFile(file, "description.txt", Encoding.UTF8.GetBytes(meta.Description));
            }
        }
        file.fileTable = file.files.Values.ToArray();

        return new LocalMod(ModLocation.Local, file, properties);

        static void AddCachedFile(TmodFile file, string path, byte[] data)
        {
            var fileName = TmodFile.Sanitize(path);
            lock (file.files)
            {
                file.files[fileName] = new TmodFile.FileEntry(fileName, -1, data.Length, data.Length, data);
            }
        }
    }
}
