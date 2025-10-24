using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShaderDecompiler;
using ShaderDecompiler.Structures;

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
    public readonly record struct ShaderVariableDescription(
        string BaseType,
        string[] VectorTypes,
        string? LargeMatrixType,
        string[] Errors,
        string[] Warnings
    )
    {
        public ShaderParameterDefinition GetDefForTypeInfo(TypeInfo typeInfo)
        {
            var errors = Errors.ToList();
            var warnings = Warnings.ToList();
            var def = new ShaderParameterDefinition(
                DotNetType: "object?",
                RealType: typeInfo.ToString(),
                IsArray: typeInfo.Elements > 1,
                Errors: errors,
                Warnings: warnings
            );

            switch (typeInfo.Class)
            {
                case ObjectClass.Object or ObjectClass.Scalar:
                    return def with { DotNetType = BaseType };

                case ObjectClass.Vector:
                    if (VectorTypes.Length >= typeInfo.Columns)
                    {
                        return def with { DotNetType = VectorTypes[typeInfo.Columns - 1] };
                    }

                    errors.Add($"Generator does not support vector of size '{typeInfo.Columns}' for type '{def.RealType}'");
                    return def;

                case ObjectClass.MatrixRows or ObjectClass.MatrixColumns:
                    if (LargeMatrixType is not null)
                    {
                        return def with { DotNetType = LargeMatrixType };
                    }

                    // errors.Add($"Generator does not support matrix of dimensions '{typeInfo.Rows}x{typeInfo.Columns}' for type '{def.RealType}'");
                    errors.Add($"Generator does not support 4x4 matrix (satisfies '{typeInfo.Rows}x{typeInfo.Columns}') for type '{def.RealType}'");
                    return def;

                // ReSharper disable once RedundantSwitchExpressionArms
                case ObjectClass.Struct:
                    errors.Add("Generator does not support struct uniforms");
                    return def;

                default:
                    errors.Add($"Generator does not know how to handle object kind: {typeInfo.Class}");
                    return def;
            }
        }
    }

    public readonly record struct ShaderParameterDefinition(
        string DotNetType,
        string RealType,
        bool IsArray,
        List<string> Errors,
        List<string> Warnings
    );

    private static readonly Dictionary<ObjectType, ShaderVariableDescription> uniform_types = new()
    {
        [ObjectType.Void] = new ShaderVariableDescription(
            BaseType: "HlslVoid",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: ["'void' is not supported, a stub uniform has been generated which will not be applied to the shader"]
        ),

        [ObjectType.Bool] = new ShaderVariableDescription(
            BaseType: "bool",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Int] = new ShaderVariableDescription(
            BaseType: "int",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Float] = new ShaderVariableDescription(
            BaseType: "float",
            VectorTypes:
            [
                "float",
                "Microsoft.Xna.Framework.Vector2",
                "Microsoft.Xna.Framework.Vector3",
                "Microsoft.Xna.Framework.Vector4",
            ],
            LargeMatrixType: "Microsoft.Xna.Framework.Matrix",
            Errors: [],
            Warnings: []
        ),

        [ObjectType.String] = new ShaderVariableDescription(
            BaseType: "HlslString",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: ["'string' is not supported, a stub uniform has been generated which will not be applied to the shader"]
        ),

        [ObjectType.Texture] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Texture1d] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Texture2d] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture2D?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Texture3d] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture3D?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [ObjectType.Texturecube] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.TextureCube?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),
    };

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
            var parameterDef = GetShaderParameterDefinition(param.Value.Type);

            foreach (var warning in parameterDef.Warnings)
            {
                sb.AppendLine($"{indent}    #warning {warning}");
            }

            foreach (var error in parameterDef.Errors)
            {
                sb.AppendLine($"{indent}    #error {error}");
            }

            var typeToUse = parameterDef.DotNetType;
            if (parameterDef.IsArray)
            {
                typeToUse += "[]?";
            }

            sb.AppendLine($"{indent}    [OriginalHlslType(\"{parameterDef.RealType}\")]");
            sb.AppendLine($"{indent}    public {typeToUse} {param.Value.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}    public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)");
        sb.AppendLine($"{indent}    {{");
        foreach (var param in effect.Parameters)
        {
            var hadExpression = false;
            foreach (var annotation in param.Annotations)
            {
                if (!(annotation.Name?.Equals("csharpExpression", StringComparison.InvariantCultureIgnoreCase) ?? false))
                {
                    continue;
                }

                if (annotation.Object is not uint[] array)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation but could not get array with index");
                    continue;
                }

                if (array.Length < 1)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation but array has no elements");
                    continue;
                }

                var objIndex = array[0];
                if (objIndex >= effect.Objects.Length)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation has object index '{objIndex}' but it's out of bounds of effect objects array ({effect.Objects.Length} elements)");
                    continue;
                }

                var obj = effect.Objects[objIndex];
                if (obj.Type != ObjectType.String || obj.Object is not string expression)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation has object index '{objIndex}' that is not string: {obj} {obj.Object}");
                    continue;
                }

                sb.AppendLine($"{indent}        parameters[\"{param.Value.Name}\"]?.SetValue({expression});");
                hadExpression = true;
            }

            if (hadExpression)
            {
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

    private static ShaderParameterDefinition GetShaderParameterDefinition(TypeInfo typeInfo)
    {
        if (!uniform_types.TryGetValue(typeInfo.Type, out var uniformType))
        {
            uniformType = new ShaderVariableDescription(
                BaseType: "object?",
                VectorTypes: [],
                LargeMatrixType: null,
                Errors: [$"Unsupported uniform object type: {typeInfo.Type} (for uniform definition '{typeInfo}')"],
                Warnings: []
            );
        }

        return uniformType.GetDefForTypeInfo(typeInfo);
    }
}
