using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Tomat.TML.Build.Common.Assets.Localization;

public static class GameCultureParser
{
    // Well-defined culture names, taken from Terraria.Localization.GameCulture.
    public static readonly string[] KNOWN_CULTURES =
    [
        "en-US",
        "de-DE",
        "it-IT",
        "fr-FR",
        "es-ES",
        "ru-RU",
        "zh-Hans",
        "pt-BR",
        "pl-PL",
    ];

    public static bool TryGetCultureAndPrefixFromPath(
        string path,
        [NotNullWhen(returnValue: true)] out string? culture,
        [NotNullWhen(returnValue: true)] out string? prefix
    )
    {
        path = Path.ChangeExtension(path, null);
        path = path.Replace("\\", "/");

        culture = null;
        prefix = null;

        var splitByFolder = path.Split('/');
        foreach (var pathPart in splitByFolder)
        {
            var splitByUnderscore = pathPart.Split('_');
            for (var underscoreSplitIndex = 0; underscoreSplitIndex < splitByUnderscore.Length; underscoreSplitIndex++)
            {
                var underscorePart = splitByUnderscore[underscoreSplitIndex];
                var parsedCulture = KNOWN_CULTURES.FirstOrDefault(culture => culture == underscorePart);
                if (parsedCulture != null)
                {
                    culture = parsedCulture;
                    continue;
                }

                if (parsedCulture != null || culture == null)
                {
                    continue;
                }

                // Some mod names have '_' in them
                prefix = string.Join("_", splitByUnderscore.Skip(underscoreSplitIndex));
                return true;
            }
        }

        if (culture == null)
        {
            return false;
        }

        prefix = string.Empty;
        return true;
    }
}
