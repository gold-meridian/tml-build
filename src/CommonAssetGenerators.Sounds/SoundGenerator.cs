using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Tomat.TML.Build.Analyzers;

internal sealed class SoundGenerator : IAssetGenerator
{
    public bool PermitsVariant(string path)
    {
        return !path.Contains("Music");
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".wav") ||
               path.RelativeOrFullPath.EndsWith(".ogg") ||
               path.RelativeOrFullPath.EndsWith(".mp3");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();

        if (asset.Path.RelativeOrFullPath.Contains("Music"))
        {
            var addMusicPath = Path.ChangeExtension(asset.Path.RelativeOrFullPath, null);
            sb.AppendLine($"{indent}private sealed class Loader : Terraria.ModLoader.ILoadable");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    public void Load(Terraria.ModLoader.Mod mod)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (!Terraria.ModLoader.MusicLoader.MusicExists(mod, \"{addMusicPath}\"))");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            Terraria.ModLoader.MusicLoader.AddMusic(mod, \"{Path.ChangeExtension(asset.Path.RelativeOrFullPath, null)}\");");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    public void Unload() {{ }}");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine("");
            sb.AppendLine($"{indent}public static Terraria.Audio.IAudioTrack Asset => lazy.Value;");
            sb.AppendLine();
            sb.AppendLine($"{indent}public static int Slot => Terraria.ModLoader.MusicLoader.GetMusicSlot(KEY);");
            sb.AppendLine();
            sb.AppendLine($"{indent}private static readonly System.Lazy<Terraria.Audio.IAudioTrack> lazy = new(() => Terraria.ModLoader.MusicLoader.GetMusic(KEY));");
        }
        else
        {
            var variantSyntax = asset.Variants.HasValue ? $", {asset.Variants.Value.Start}, {asset.Variants.Value.Count}" : "";
            sb.AppendLine($"{indent}public static Terraria.Audio.SoundStyle Asset => new Terraria.Audio.SoundStyle(\"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath, null)}\"{variantSyntax});");
        }

        sb.AppendLine();
        return sb.ToString().TrimEnd();
    }
}

internal static class Loader
{
    [ModuleInitializer]
    public static void LoadGenerator()
    {
        AssetGeneratorProvider.AddGenerator<SoundGenerator>();
    }
}
