using NUnit.Framework;
using Tomat.TML.Build.Common.Assets.Localization;

namespace Tomat.TML.Build.Tests.Localization;

[TestFixture]
public static class LocalizationParsingTests
{
    private const string arbitrary_path_root = "C:/some/arbitrary/path/parts/";
    private const string generic_mod_path = "MyMod/";
    private const string generic_mod_path_full = arbitrary_path_root + "MyMod/";
    private const string underscore_mod_path = "My_Mod/";
    private const string underscore_mod_path_full = arbitrary_path_root + "My_Mod/";

    private static readonly string[] path_prefixes =
    [
        string.Empty, // For base test cases
        generic_mod_path,
        generic_mod_path_full,
        underscore_mod_path,
        underscore_mod_path_full,
    ];

    // tModLoader-provided examples in documentation.
    [TestCase("Localization/en-US_Mods.ExampleMod.hjson", "en-US", "Mods.ExampleMod")]
    [TestCase("Localization/en-US/Mods.ExampleMod.hjson", "en-US", "Mods.ExampleMod")]
    [TestCase("en-US_Mods.ExampleMod.hjson", "en-US", "Mods.ExampleMod")]
    [TestCase("en-US/Mods.ExampleMod.hjson", "en-US", "Mods.ExampleMod")]
    // Nightshade cases
    [TestCase("Localization/en-US/en-US.hjson", "en-US", "")]
    [TestCase("Localization/en-US/en-US_Mods.Nightshade.Items.hjson", "en-US", "Mods.Nightshade.Items")]
    [TestCase("Localization/en-US/en-US_Mods.Nightshade.hjson", "en-US", "Mods.Nightshade")]
    // Destructor-Ben original fail cases
    [TestCase("Localization/en-US/Mods.AccessoriesPlus.Configs.hjson", "en-US", "Mods.AccessoriesPlus.Configs")]
    [TestCase("Localization/en-US/Mods.AccessoriesPlus.hjson", "en-US", "Mods.AccessoriesPlus")]
    public static void EnsureCultureAndPrefixAreFound(
        string path,
        string culture,
        string prefix
    )
    {
        foreach (var pathPrefix in path_prefixes)
        {
            var success = GameCultureParser.TryGetCultureAndPrefixFromPath(
                pathPrefix + path,
                out var parsedCulture,
                out var parsedPrefix
            );

            Assert.Multiple(
                () =>
                {
                    Assert.That(success, Is.True);
                    Assert.That(parsedCulture, Is.EqualTo(culture));
                    Assert.That(parsedPrefix, Is.EqualTo(prefix));
                }
            );
        }
    }
}
