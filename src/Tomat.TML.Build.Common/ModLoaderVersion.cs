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
    private static readonly Lazy<ModLoaderVersion> steam_lazy   = new();
    private static readonly Lazy<ModLoaderVersion> dev_lazy     = new();
    private static readonly Lazy<ModLoaderVersion> stable_lazy  = new();
    private static readonly Lazy<ModLoaderVersion> preview_lazy = new();

    public static readonly ModLoaderVersion UNKNOWN = new(0, 0, 0, 0);

    public static ModLoaderVersion Steam => steam_lazy.Value;

    public static ModLoaderVersion Dev => dev_lazy.Value;

    public static ModLoaderVersion Stable => stable_lazy.Value;

    public static ModLoaderVersion Preview => preview_lazy.Value;

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}.{Build}";
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