using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GoldMeridian.PaintLabel;
using GoldMeridian.PaintLabel.IO;

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
        public ShaderParameterDefinition GetDefForTypeInfo(HlslSymbolTypeInfo typeInfo)
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

            switch (typeInfo.ParameterClass)
            {
                case HlslSymbolClass.Object or HlslSymbolClass.Scalar:
                    return def with { DotNetType = BaseType };

                case HlslSymbolClass.Vector:
                    if (VectorTypes.Length >= typeInfo.Columns)
                    {
                        return def with { DotNetType = VectorTypes[typeInfo.Columns - 1] };
                    }

                    errors.Add($"Generator does not support vector of size '{typeInfo.Columns}' for type '{def.RealType}'");
                    return def;

                case HlslSymbolClass.MatrixRows or HlslSymbolClass.MatrixColumns:
                    if (LargeMatrixType is not null)
                    {
                        return def with { DotNetType = LargeMatrixType };
                    }

                    // errors.Add($"Generator does not support matrix of dimensions '{typeInfo.Rows}x{typeInfo.Columns}' for type '{def.RealType}'");
                    errors.Add($"Generator does not support 4x4 matrix (satisfies '{typeInfo.Rows}x{typeInfo.Columns}') for type '{def.RealType}'");
                    return def;

                // ReSharper disable once RedundantSwitchExpressionArms
                case HlslSymbolClass.Struct:
                    errors.Add("Generator does not support struct uniforms");
                    return def;

                default:
                    errors.Add($"Generator does not know how to handle object kind: {typeInfo.ParameterClass}");
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

    private static readonly Dictionary<HlslSymbolType, ShaderVariableDescription> uniform_types = new()
    {
        [HlslSymbolType.Void] = new ShaderVariableDescription(
            BaseType: "HlslVoid",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: ["'void' is not supported, a stub uniform has been generated which will not be applied to the shader"]
        ),

        [HlslSymbolType.Bool] = new ShaderVariableDescription(
            BaseType: "bool",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Int] = new ShaderVariableDescription(
            BaseType: "int",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Float] = new ShaderVariableDescription(
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

        [HlslSymbolType.String] = new ShaderVariableDescription(
            BaseType: "HlslString",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: ["'string' is not supported, a stub uniform has been generated which will not be applied to the shader"]
        ),

        [HlslSymbolType.Texture] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Texture1D] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Texture2D] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture2D?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Texture3D] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.Texture3D?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.TextureCube] = new ShaderVariableDescription(
            BaseType: "Microsoft.Xna.Framework.Graphics.TextureCube?",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Sampler] = new ShaderVariableDescription(
            BaseType: "HlslSampler",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Sampler1D] = new ShaderVariableDescription(
            BaseType: "HlslSampler1D",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Sampler2D] = new ShaderVariableDescription(
            BaseType: "HlslSampler2D",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.Sampler3D] = new ShaderVariableDescription(
            BaseType: "HlslSampler3D",
            VectorTypes: [],
            LargeMatrixType: null,
            Errors: [],
            Warnings: []
        ),

        [HlslSymbolType.SamplerCube] = new ShaderVariableDescription(
            BaseType: "HlslSamplerCube",
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
        // TODO: Support XNB again.
        return path.RelativeOrFullPath.EndsWith(".fxc");
        // || path.RelativeOrFullPath.EndsWith(".xnb");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        const string type = "Microsoft.Xna.Framework.Graphics.Effect";

        var sb = new StringBuilder();

#pragma warning disable RS1035
        HlslEffect effect;
        try
        {
            using var fs = File.OpenRead(asset.Path.FullPath);
            using var reader = new BinaryReader(fs);
            effect = EffectReader.ReadEffect(reader);
        }
        catch (Exception e)
        {
            return $"{indent}#error Failed to parse effect file \"{asset.Path.FullPath}\": {e.Message}";
        }
#pragma warning restore RS1035

        if (effect.HasErrors)
        {
            sb.AppendLine($"{indent}#error Encountered errors parsing effect file \"{asset.Path.FullPath}\":");

            foreach (var error in effect.Errors)
            {
                sb.AppendLine($"{indent}#error {error.Message}");
            }
        }

        sb.AppendLine($"{indent}public sealed class Parameters : IShaderParameters");
        sb.AppendLine($"{indent}{{");

        var samplersWithRegisters = new HashSet<string>();
        var texturesWithRegisters = new HashSet<string>();

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

            if (typeToUse.StartsWith("HlslSampler"))
            {
                samplersWithRegisters.Add(param.Value.Name!);
            }
            else if (typeToUse.StartsWith("Microsoft.Xna.Framework.Graphics.Texture"))
            {
                texturesWithRegisters.Add(param.Value.Name!);
            }

            sb.AppendLine($"{indent}    [OriginalHlslType(\"{parameterDef.RealType}\")]");
            sb.AppendLine($"{indent}    public {typeToUse} {param.Value.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.Dictionary<string, int>> register_map = new()");
        sb.AppendLine($"{indent}    {{");

        // should always be 1 but whatever
        if (effect.Techniques.Length > 0)
        {
            var technique = effect.Techniques[0];

            var registersPerPass = new Dictionary<string, Dictionary<string, int>>();

            foreach (var pass in technique.Passes)
            {
                if (pass.Name is null)
                {
                    continue;
                }

                if (!registersPerPass.TryGetValue(pass.Name, out var passRegisters))
                {
                    registersPerPass[pass.Name] = passRegisters = [];
                }

                var shaders = pass.States.Where(x => x.Type is HlslRenderStateType.PixelShader or HlslRenderStateType.VertexShader);
                foreach (var shader in shaders)
                {
                    if (!shader.Value.Values.TryGetArray<int>(out var ints))
                    {
                        continue;
                    }

                    try
                    {
                        var obj = effect.Objects[ints[0]];
                        if (obj.Type is not HlslSymbolType.PixelShader and not HlslSymbolType.VertexShader)
                        {
                            continue;
                        }

                        if (!obj.TryGetShader(out var objShader))
                        {
                            continue;
                        }

                        foreach (var constant in objShader.Value.Constants)
                        {
                            if (constant.Name is null)
                            {
                                continue;
                            }

                            passRegisters[constant.Name] = constant.RegIndex;
                        }
                    }
                    catch
                    {
                        // TODO: error handling perhaps
                        continue;
                    }
                }
            }

            foreach (var kvp in registersPerPass)
            {
                var passName = kvp.Key;
                var registerMap = kvp.Value;

                sb.AppendLine($"{indent}        [\"{passName}\"] = new()");
                sb.AppendLine($"{indent}        {{");

                foreach (var kvp2 in registerMap)
                {
                    var uniformName = kvp2.Key;
                    var register = kvp2.Value;

                    sb.AppendLine($"{indent}            [\"{uniformName}\"] = {register},");
                }

                sb.AppendLine($"{indent}        }},");
            }
        }

        sb.AppendLine($"{indent}    }};");
        sb.AppendLine();

        sb.AppendLine($"{indent}    public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters, string passName)");
        sb.AppendLine($"{indent}    {{");
        foreach (var param in effect.Parameters)
        {
            var hadExpression = false;
            foreach (var annotation in param.Annotations)
            {
                var value = annotation.Value;

                if (!(value.Name?.Equals("csharpExpression", StringComparison.InvariantCultureIgnoreCase) ?? false))
                {
                    continue;
                }

                if (!value.Values.TryGetArray<int>(out var ints))
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation but could not get array with index");
                    continue;
                }

                if (ints.Length < 1)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation but array has no elements");
                    continue;
                }

                var objIndex = ints[0];
                if (objIndex >= effect.Objects.Length)
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation has object index '{objIndex}' but it's out of bounds of effect objects array ({effect.Objects.Length} elements)");
                    continue;
                }

                var obj = effect.Objects[objIndex];
                if (obj.Type != HlslSymbolType.String || obj.Value is not HlslEffectString { String: { } expression })
                {
                    sb.AppendLine($"{indent}        #error Parameter '{param.Value.Name}' has 'csharpExpression' annotation has object index '{objIndex}' that is not string: {obj} {obj.Value}");
                    continue;
                }

                sb.AppendLine($"{indent}        parameters[\"{param.Value.Name}\"]?.SetValue({expression});");
                hadExpression = true;
            }

            if (hadExpression)
            {
                continue;
            }

            if (samplersWithRegisters.Contains(param.Value.Name!))
            {
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            if (register_map.TryGetValue(passName, out var passRegisters) && passRegisters.TryGetValue(\"{param.Value.Name}\", out var register))");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                if ({param.Value.Name}.Texture is not null)");
                sb.AppendLine($"{indent}                {{");
                sb.AppendLine($"{indent}                    global::Terraria.Main.graphics.GraphicsDevice.Textures[register] = {param.Value.Name}.Texture;");
                sb.AppendLine($"{indent}                }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}                if ({param.Value.Name}.Sampler is not null)");
                sb.AppendLine($"{indent}                {{");
                sb.AppendLine($"{indent}                    global::Terraria.Main.graphics.GraphicsDevice.SamplerStates[register] = {param.Value.Name}.Sampler;");
                sb.AppendLine($"{indent}                }}");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}        }}");
            }
            else if (texturesWithRegisters.Contains(param.Value.Name!))
            {
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            if (register_map.TryGetValue(passName, out var passRegisters) && passRegisters.TryGetValue(\"{param.Value.Name}\", out var register))");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                global::Terraria.Main.graphics.GraphicsDevice.Textures[register] = {param.Value.Name};");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}            else");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                parameters[\"{param.Value.Name}\"]?.SetValue({param.Value.Name});");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}        }}");
            }
            else
            {
                sb.AppendLine($"{indent}        parameters[\"{param.Value.Name}\"]?.SetValue({param.Value.Name});");
            }
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

    private static ShaderParameterDefinition GetShaderParameterDefinition(HlslSymbolTypeInfo typeInfo)
    {
        if (!uniform_types.TryGetValue(typeInfo.ParameterType, out var uniformType))
        {
            uniformType = new ShaderVariableDescription(
                BaseType: "object?",
                VectorTypes: [],
                LargeMatrixType: null,
                Errors: [$"Unsupported uniform object type: {typeInfo.ParameterType} (for uniform definition '{typeInfo}')"],
                Warnings: []
            );
        }

        return uniformType.GetDefForTypeInfo(typeInfo);
    }
}

internal sealed class ModelReference : IAssetReference
{
    public bool PermitsVariant(string path)
    {
        return false;
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".obj");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}public static ReLogic.Content.Asset<{assemblyName}.Core.ObjModel> Asset => lazy.Value;");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static readonly System.Lazy<ReLogic.Content.Asset<{assemblyName}.Core.ObjModel>> lazy = new(() => Terraria.ModLoader.ModContent.Request<{assemblyName}.Core.ObjModel>(KEY));");

        return sb.ToString().TrimEnd();
    }
}
