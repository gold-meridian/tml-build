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
            // Implementation provided by Qther <qther@tuta.io>:
            // https://github.com/gold-meridian/tml-build/issues/2
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var appDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                yield return Path.Combine(appDir, $"{gameName}.app");
            }

            yield return Path.Combine(dataDir, "Steam", "steamapps", "common", gameName);
            yield return Path.Combine(home, ".steam", "steam", "steamapps", "common", gameName);
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam", "steamapps", "common", gameName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", gameName);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", gameName);
        }
    }
}
