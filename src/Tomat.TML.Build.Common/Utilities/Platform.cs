using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Tomat.TML.Build.Common.Utilities;

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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Environment.GetEnvironmentVariable("HOME");
            var unixHome = Environment.GetEnvironmentVariable("HOME");
            var home = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? xdgDataHome : unixHome;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return $"{home}/Applications/{gameName}.app/";
                yield return $"{home}/Library/Application Support/Steam/steamapps/common/{gameName}/";
            }

            yield return $"{home}/.steam/steam/steamapps/common/{gameName}";
            yield return $"{home}/.local/share/Steam/steamapps/common/{gameName}";
            yield return $"{home}/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/{gameName}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", gameName);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", gameName);
        }
    }
}
