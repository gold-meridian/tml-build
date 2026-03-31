using System;
using System.Collections.Generic;
using System.IO;
using Terraria;

namespace Tomat.TML.ClientBootstrap.Framework;

public readonly record struct LaunchPluginMetadata(
    string UniqueId,
    string DisplayName,
    string Version,
    string Authors,
    string Description,
    Func<Stream?> IconProvider
);

/// <summary>
///     An arbitrary plugin to be executed upon game launch.
/// </summary>
public abstract class LaunchPlugin
{
    public abstract LaunchPluginMetadata Metadata { get; }

    /// <summary>
    ///     Called immediately after basic initialization, before the game is
    ///     launched.
    /// </summary>
    /// <param name="ctx">The launch context.</param>
    /// <param name="plugins">All plugins being loaded.</param>
    public virtual void Load(LaunchContext ctx, List<LaunchPlugin> plugins) { }

    /// <summary>
    ///     If the plugin is enabled, ran during the initial patching stage on a
    ///     secondary thread.
    /// </summary>
    /// <param name="ctx">The launch context.</param>
    public virtual void ApplyPatches(LaunchContext ctx) { }

#region Game-specific
    /// <summary>
    ///     Ran at the start of <see cref="Main.LoadContent" />.
    /// </summary>
    /// <remarks>
    ///     Patches may or may not have finished applying by now.
    /// </remarks>
    /// <param name="ctx">The launch context.</param>
    public virtual void LoadContent(LaunchContext ctx) { }
#endregion
}
