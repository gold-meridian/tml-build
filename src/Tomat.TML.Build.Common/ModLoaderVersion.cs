using System;

namespace Tomat.TML.Build.Common;

/// <summary>
///     Represents a tModLoader version.
/// </summary>
/// <param name="Major"></param>
/// <param name="Minor"></param>
/// <param name="Patch"></param>
/// <param name="Build"></param>
public readonly record struct ModLoaderVersion(int Major, int Minor, int Patch, int Build) : IComparable<ModLoaderVersion>
{
    private static readonly Lazy<ModLoaderVersion> stable_lazy  = new();
    private static readonly Lazy<ModLoaderVersion> preview_lazy = new();

    public static readonly ModLoaderVersion UNKNOWN = new(0, 0, 0, 0);

    public static ModLoaderVersion Stable => ModLoaderVersionManager.Cache.StableVersion;

    public static ModLoaderVersion Preview => ModLoaderVersionManager.Cache.PreviewVersion;

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}.{Build}";
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

#region Comparison
    public int CompareTo(ModLoaderVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        var patchComparison = Patch.CompareTo(other.Patch);
        if (patchComparison != 0)
        {
            return patchComparison;
        }

        return Build.CompareTo(other.Build);
    }

    public static bool operator <(ModLoaderVersion left, ModLoaderVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ModLoaderVersion left, ModLoaderVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ModLoaderVersion left, ModLoaderVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ModLoaderVersion left, ModLoaderVersion right)
    {
        return left.CompareTo(right) >= 0;
    }
#endregion
}