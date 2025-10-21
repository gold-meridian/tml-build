using Terraria;

namespace Tomat.TML.ClientBootstrap.Framework;

/// <summary>
///     An arbitrary plugin to be executed upon game launch.
/// </summary>
public abstract class LaunchPlugin
{
    /// <summary>
    ///     The unique identifier of the plugin, should be all lowercase.
    ///     <br />
    ///     Typically, in the format of <c>author.plugin</c>.
    /// </summary>
    public abstract string UniqueId { get; }

    /// <summary>
    ///     Called immediately after basic initialization, before the game is
    ///     launched.
    /// </summary>
    /// <param name="ctx">The launch context.</param>
    public virtual void Load(LaunchContext ctx) { }

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
