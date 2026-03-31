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
using Terraria.UI;
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

    private static readonly Dictionary<string, LocalMod> local_mod_map = [];
    private static readonly HashSet<LocalMod> local_mods = [];

    public override void Load(LaunchContext ctx, List<LaunchPlugin> plugins)
    {
        base.Load(ctx, plugins);

        mods.Add(MakeTmlMetadata(in ctx));
        mods.Add(MakeTmlBuildMetadata(in ctx));
        mods.AddRange(plugins.Select(x => x.Metadata));

        return;

        static LaunchPluginMetadata MakeTmlMetadata(in LaunchContext ctx)
        {
            return new LaunchPluginMetadata(
                UniqueId: "tml",
                DisplayName: $"[tml-build] tModLoader ({ctx.TmlVersion})",
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

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.ToggleEnabled), BindingFlags.NonPublic | BindingFlags.Instance)!,
            MouseEvent_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.Enable), BindingFlags.NonPublic | BindingFlags.Instance)!,
            NoParams_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.Disable), BindingFlags.NonPublic | BindingFlags.Instance)!,
            NoParams_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.UpdateUIForEnabledChange), BindingFlags.NonPublic | BindingFlags.Instance)!,
            NoParams_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.EnableDependencies), BindingFlags.NonPublic | BindingFlags.Instance)!,
            NoParams_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.DisableDependents), BindingFlags.NonPublic | BindingFlags.Instance)!,
            NoParams_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.OpenConfig), BindingFlags.NonPublic | BindingFlags.Instance)!,
            MouseEvent_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.QuickModDelete), BindingFlags.NonPublic | BindingFlags.Instance)!,
            MouseEvent_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.DeleteMod), BindingFlags.NonPublic | BindingFlags.Instance)!,
            MouseEvent_DoNothing
        );

        ctx.Hooks.Add(
            typeof(UIModItem).GetMethod(nameof(UIModItem.OnInitialize), BindingFlags.Public | BindingFlags.Instance)!,
            OnInitialize_RemoveBadElements
        );

        ctx.Hooks.Add(
            typeof(LocalMod).GetMethod($"get_{nameof(LocalMod.Enabled)}", BindingFlags.Public | BindingFlags.Instance)!,
            Enabled_Always
        );

        return;

        static bool Enabled_Always(Func<LocalMod, bool> orig, LocalMod self)
        {
            return local_mods.Contains(self) || orig(self);
        }

        static void OnInitialize_RemoveBadElements(Action<UIModItem> orig, UIModItem self)
        {
            orig(self);

            if (!IsTmlBuildMod(self))
            {
                return;
            }

            TryRemoveChild(self._uiModStateText);
            TryRemoveChild(self._configButton);
            TryRemoveChild(self._modReferenceIcon);
            TryRemoveChild(self._translationModIcon);
            TryRemoveChild(self._deleteModButton);
            TryRemoveChild(self.updatedModDot);
            return;

            void TryRemoveChild(UIElement? child)
            {
                if (child is null)
                {
                    return;
                }

                self.RemoveChild(child);
            }
        }

        static void Update_PopulateResult(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchCallvirt<Task<List<UIModItem>>>($"get_{nameof(Task<>.Result)}"));
            c.EmitDelegate(InsertOurMods);
        }

        static void MouseEvent_DoNothing(Action<UIModItem, UIMouseEvent, UIElement> orig, UIModItem self, UIMouseEvent evt, UIElement listeningElement)
        {
            if (IsTmlBuildMod(self))
            {
                return;
            }

            orig(self, evt, listeningElement);
        }

        static void NoParams_DoNothing(Action<UIModItem> orig, UIModItem self)
        {
            if (IsTmlBuildMod(self))
            {
                return;
            }

            orig(self);
        }
    }

    private static bool IsTmlBuildMod(UIModItem modItem)
    {
        return local_mods.Contains(modItem._mod);
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
        if (!local_mod_map.TryGetValue(meta.UniqueId, out var localMod))
        {
            local_mod_map[meta.UniqueId] = localMod = MakeLocalMod(in meta);
        }

        local_mods.Add(localMod);
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
