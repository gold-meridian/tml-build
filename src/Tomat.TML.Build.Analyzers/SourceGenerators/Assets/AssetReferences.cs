using System;
using System.IO;
using System.Linq;
using System.Text;
using ShaderDecompiler;

namespace Tomat.TML.Build.Analyzers.SourceGenerators.Assets;

internal sealed class AssetPath(string fullPath, string? relativePath)
{
    public string FullPath => fullPath;

    public string? RelativePath { get; set; } = relativePath;

    public string RelativeOrFullPath => RelativePath ?? FullPath;
}

internal interface IAssetReference
{
    bool PermitsVariant(string path);

    bool Eligible(AssetPath path);

    string GenerateCode(string assemblyName, AssetFile asset, string indent);
}

internal sealed class TextureReference : IAssetReference
{
    public bool PermitsVariant(string path)
    {
        return false;
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".png") ||
               path.RelativeOrFullPath.EndsWith(".rawimg");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));");

        return sb.ToString().TrimEnd();
    }
}

internal sealed class SoundReference : IAssetReference
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

internal sealed class EffectReference : IAssetReference
{
    public bool PermitsVariant(string path)
    {
        return false;
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".fxc")
            || path.RelativeOrFullPath.EndsWith(".xnb");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        const string type = "Microsoft.Xna.Framework.Graphics.Effect";

        var sb = new StringBuilder();

        var effect = Effect.ReadXnbOrFxc(asset.Path.FullPath, out _);

        sb.AppendLine($"{indent}public sealed class Parameters : IShaderParameters");
        sb.AppendLine($"{indent}{{");

        foreach (var param in effect.Parameters)
        {
            var uniformType = GetUniformType(param.Value.Type.ToString());
            sb.AppendLine($"{indent}    public {uniformType} {param.Value.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}    public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)");
        sb.AppendLine($"{indent}    {{");
        foreach (var param in effect.Parameters)
        {
            if (param.Value.Name == "uTime")
            {
                sb.AppendLine($"{indent}        parameters[\"{param.Value.Name}\"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);");
                continue;
            }

            sb.AppendLine($"{indent}        parameters[\"{param.Value.Name}\"]?.SetValue({param.Value.Name});");
        }

        sb.AppendLine($"{indent}    }}");

        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}public static ReLogic.Content.Asset<{type}> Asset => lazy.Value;");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static readonly System.Lazy<ReLogic.Content.Asset<{type}>> lazy = new(() => Terraria.ModLoader.ModContent.Request<{type}>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));");
        sb.AppendLine();

        foreach (var passes in effect.Techniques.SelectMany(x => x.Passes))
        {
            sb.AppendLine($"{indent}public static WrapperShaderData<Parameters> Create{passes.Name}()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    return new WrapperShaderData<Parameters>(Asset, \"{passes.Name}\");");
            sb.AppendLine($"{indent}}}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetUniformType(string uniformType)
    {
        var isArray = false;
        if (uniformType.Contains("["))
        {
            var baseType = uniformType[..uniformType.IndexOf('[')];
            uniformType = baseType;
            isArray = true;
        }

        var finalType = uniformType switch
        {
            "float" => "float",
            "float2" => "Microsoft.Xna.Framework.Vector2",
            "float3" => "Microsoft.Xna.Framework.Vector3",
            "float4" => "Microsoft.Xna.Framework.Vector4",
            "float4x4" => "Microsoft.Xna.Framework.Matrix",
            "matrix" => "Microsoft.Xna.Framework.Matrix",
            "sampler" => "Microsoft.Xna.Framework.Graphics.Texture2D?",
            "sampler2D" => "Microsoft.Xna.Framework.Graphics.Texture2D?",
            "texture" => "Microsoft.Xna.Framework.Graphics.Texture2D?",
            "texture2D" => "Microsoft.Xna.Framework.Graphics.Texture2D?",
            "bool" => "bool",
            _ => throw new InvalidOperationException("Unsupported uniform type: " + uniformType),
        };

        if (isArray)
        {
            finalType += "[]?";
        }

        return finalType;
    }
}
