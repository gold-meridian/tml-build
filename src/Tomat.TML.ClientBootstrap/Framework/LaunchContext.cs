using System.Collections.Generic;
using System.Reflection;

namespace Tomat.TML.ClientBootstrap.Framework;

/// <summary>
///     The launch mode.
/// </summary>
public enum LaunchMode
{
    /// <summary>
    ///     Launches a client process.
    /// </summary>
    Client,

    /// <summary>
    ///     Launches a server process.
    /// </summary>
    Server,
}

/// <summary>
///     The context behind a launch request.
/// </summary>
/// <param name="BootstrapDirectory">The directory of the bootstrapper.</param>
/// <param name="GameDirectory">The directory of the game.</param>
/// <param name="GameBinaryName">
///     The path part of the main game assembly to launch from the game directory.
/// </param>
/// <param name="LaunchMode">The launch mode.</param>
/// <param name="RequestedModName">
///     The mod specifically requesting the launch.
/// </param>
/// <param name="RequestedFeatures">An array of requested feature names.</param>
/// <param name="GameLaunchArguments">Pass-through launch arguments.</param>
/// <param name="PluginArguments">Plugin command-line arguments.</param>
/// <param name="GameAssembly">The game assembly.</param>
public readonly record struct LaunchContext(
    string BootstrapDirectory,
    string GameDirectory,
    string GameBinaryName,
    LaunchMode LaunchMode,
    string? RequestedModName,
    string[] RequestedFeatures,
    string[] GameLaunchArguments,
    Dictionary<string, Dictionary<string, string?>> PluginArguments,
    Assembly GameAssembly
);
