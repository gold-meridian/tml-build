using System;

namespace Tomat.TML.Build.Common;

/// <summary>
///     Represents a tModLoader version.
/// </summary>
/// <param name="Major"></param>
/// <param name="Minor"></param>
/// <param name="Patch"></param>
/// <param name="Build"></param>
public readonly record struct ModLoaderVersion(
    int Major,
    int Minor,
    int Patch,
    int Build
)
{
    public static ModLoaderVersion Unknown { get; } = default;

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}.{Build}";
    }

    public Version ToSystemVersion()
    {
        return new Version(Major, Minor, Patch, Build);
    }

    public static bool TryParse(string text, out ModLoaderVersion version)
    {
        text = text.TrimStart('v');
        if (Version.TryParse(text, out var sysVersion))
        {
            version = new ModLoaderVersion(sysVersion.Major, sysVersion.Minor, sysVersion.Build, sysVersion.Revision);
            return true;
        }

        version = default(ModLoaderVersion);
        return false;
    }
}
