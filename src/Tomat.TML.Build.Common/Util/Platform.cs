using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tomat.TML.Build.Common.Util;

/// <summary>
///     Platform abstractions.
/// </summary>
internal static class Platform
{
    /// <summary>
    ///     Gets the directory to our application data.
    /// </summary>
    public static string GetAppDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tml-build");
    }

    public static string? GetSteamGamePath(string gameName)
    {
        return GetSteamGamePaths(gameName).FirstOrDefault(Directory.Exists);
    }

    private static IEnumerable<string> GetSteamGamePaths(string gameName)
    {
        if (Environment.GetEnvironmentVariable("HOME") is { } home)
        {
            yield return $"{home}/.steam/steam/steamapps/common/{gameName}";
            yield return $"{home}/.local/share/Steam/steamapps/common/{gameName}";
            yield return $"{home}/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/{gameName}";

            // TODO: what does it look like on macOS?
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var steamPath = Registry.CurrentUser.GetValue(@"Software\Valve\Steam\SteamPath") as string
                         ?? Registry.LocalMachine.GetValue(@"Software\Valve\Steam\SteamPath") as string;

            if (steamPath is not null)
            {
                yield return Path.Combine(steamPath, "steamapps", "common", gameName);
            }

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", gameName);
        }
    }
}
