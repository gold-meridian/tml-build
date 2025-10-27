using System;
using System.Runtime.InteropServices;

namespace Tomat.TML.ClientBootstrap.Framework;

internal static class ProcessIdentifier
{
    public static string OsId { get; } = OperatingSystem.IsWindows()
        ? "win"
        : OperatingSystem.IsLinux()
            ? "linux"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "unknown";

    public static string ArchId { get; } = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public static string Rid => $"{OsId}-{ArchId}";

    public static bool Compatible(string? runtime)
    {
        return string.IsNullOrEmpty(runtime) || Rid.Equals(runtime, StringComparison.InvariantCultureIgnoreCase);
    }
}
