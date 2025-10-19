using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace Tomat.Files.Tmod;

partial class BuildProperties
{
    public readonly record struct Diagnostic(
        string Path,
        (int Line, int? Column)? Location,
        string MessageType,
        string Code,
        string Message,
        bool FatalError
    )
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            {
                sb.Append(Path);

                if (Location.HasValue)
                {
                    sb.Append(
                        Location.Value.Column.HasValue
                            ? $"({Location.Value.Line},{Location.Value.Column.Value})"
                            : $"({Location.Value.Line})"
                    );
                }

                sb.Append($": {MessageType} {Code}: {Message}");
            }
            return sb.ToString();
        }
    }

    public static BuildProperties ReadBuildInfo(string buildFile, out List<Diagnostic> diagnostics, out bool hasErrors)
    {
        var properties = new BuildProperties();

        diagnostics = [];
        hasErrors = false;

        var lines = File.ReadAllLines(buildFile);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Divergence from tML: trim lines.
            line = line.Trim();

            // Divergence from tML: explicitly treat lines starting with '#' as
            //                      a comment.
            if (line.StartsWith("#"))
            {
                continue;
            }

            var split = line.IndexOf('=');
            if (split == -1)
            {
                hasErrors = true;
                diagnostics.Add(
                    new Diagnostic(
                        buildFile,
                        (i + 1, null),
                        "error",
                        "BUILDTXT",
                        $"Found property with no value: {line}",
                        FatalError: true
                    )
                );
                continue;
            }

            var property = line[..split].Trim();
            var value = line[(split + 1)..].Trim();
            if (value.Length == 0)
            {
                hasErrors = true;
                diagnostics.Add(
                    new Diagnostic(
                        buildFile,
                        (i + 1, split),
                        "error",
                        "BUILDTXT",
                        $"Found property with no value: {property}",
                        FatalError: true
                    )
                );
                continue;
            }

            foreach (var diag in ProcessProperty(properties, property, value, buildFile, i + 1))
            {
                hasErrors |= diag.FatalError;
                diagnostics.Add(diag);
            }
        }

        VerifyRefs(properties.GetReferenceNames(true).ToList());
        properties.SortAfter = GetDistinctRefs();

        return properties;

        // Adds (mod|weak)References that are not in sortBefore to sortAfter
        string[] GetDistinctRefs()
        {
            return properties.GetReferenceNames(true)
                             .Where(dep => !properties.SortBefore.Contains(dep))
                             .Concat(properties.SortAfter).Distinct().ToArray();
        }

        static void VerifyRefs(List<string> refs)
        {
            if (refs.Distinct().Count() != refs.Count)
            {
                throw new DuplicateNameException("Duplicate mod or weak references.");
            }
        }
    }

    private static IEnumerable<Diagnostic> ProcessProperty(BuildProperties properties, string property, string value, string path, int line)
    {
        var diags = new List<Diagnostic>();

        switch (property)
        {
            case "dllReferences":
                properties.DllReferences = ReadList(value).ToList();
                break;

            case "modReferences":
                var modRefs = ReadList(value);
                var modReferences = new List<ModReference>();
                foreach (var modRef in modRefs)
                {
                    try
                    {
                        modReferences.Add(ModReference.Parse(modRef));
                    }
                    catch (Exception e)
                    {
                        diags.Add(
                            new Diagnostic(
                                path,
                                (line, null),
                                "error",
                                "BUILDTXT",
                                "Failed to parse mod reference: " + e.Message,
                                FatalError: true
                            )
                        );
                    }
                }

                properties.ModReferences = modReferences;
                break;

            case "weakReferences":
                var weakRefs = ReadList(value);
                var weakReferences = new List<ModReference>();
                foreach (var weakRef in weakRefs)
                {
                    try
                    {
                        weakReferences.Add(ModReference.Parse(weakRef));
                    }
                    catch (Exception e)
                    {
                        diags.Add(
                            new Diagnostic(
                                path,
                                (line, null),
                                "error",
                                "BUILDTXT",
                                "Failed to parse weak reference: " + e.Message,
                                FatalError: true
                            )
                        );
                    }
                }

                properties.WeakReferences = weakReferences;
                break;

            case "sortBefore":
                properties.SortBefore = ReadList(value).ToArray();
                break;

            case "sortAfter":
                properties.SortAfter = ReadList(value).ToArray();
                break;

            case "author":
                properties.Author = value;
                break;

            case "version":
                try
                {
                    properties.Version = new Version(value);
                }
                catch (Exception e)
                {
                    diags.Add(
                        new Diagnostic(
                            path,
                            (line, null),
                            "error",
                            "BUILDTXT",
                            "Failed to parse version: " + e.Message,
                            FatalError: true
                        )
                    );
                }

                break;

            case "displayName":
                properties.DisplayName = value;
                break;

            case "homepage":
                properties.Homepage = value;
                break;

            case "playableOnPreview":
                EnsureBooleanValue("playableOnPreview", value);
                properties.PlayableOnPreview = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "translationMod":
                EnsureBooleanValue("translationMod", value);
                properties.TranslationMod = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "hideCode":
                EnsureBooleanValue("hideCode", value);
                properties.HideCode = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "hideResources":
                EnsureBooleanValue("hideResources", value);
                properties.HideResources = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "includeSource":
                EnsureBooleanValue("includeSource", value);
                properties.IncludeSource = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "buildIgnore":
                EnsureBooleanValue("buildIgnore", value);
                properties.BuildIgnores = value.Split(',')
                                               .Select(s => s.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
                                               .Where(s => s.Length > 0)
                                               .ToArray();
                break;

            case "side":
                if (!Enum.TryParse<ModSide>(value, true, out var side))
                {
                    diags.Add(
                        new Diagnostic(
                            path,
                            (line, null),
                            "error",
                            "BUILDTXT",
                            "Failed to parse mod side (must be one of Both, Client, Server, NoSync): " + value,
                            FatalError: true
                        )
                    );
                }

                properties.Side = side;
                break;
        }

        return diags;

        void EnsureBooleanValue(string propertyName, string valueToCheck)
        {
            if (valueToCheck.ToLowerInvariant() is "true" or "false")
            {
                return;
            }

            // Just a warning because the tML parser only checks if something is
            // equal to "true", treating all other values as false.
            diags.Add(
                new Diagnostic(
                    path,
                    (line, null),
                    "warning",
                    "BUILDTXT",
                    $"Property \"{propertyName}\" expects boolean values \"true\" or \"false\", got: {valueToCheck}",
                    FatalError: false
                )
            );
        }

        static IEnumerable<string> ReadList(string value)
        {
            return value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
        }
    }

    public byte[] ToBytes(string buildVersion)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        if (DllReferences.Count > 0)
        {
            writer.Write("dllReferences");
            WriteList(DllReferences, writer);
        }

        if (ModReferences.Count > 0)
        {
            writer.Write("modReferences");
            WriteList(ModReferences, writer);
        }

        if (WeakReferences.Count > 0)
        {
            writer.Write("weakReferences");
            WriteList(WeakReferences, writer);
        }

        if (SortAfter.Length > 0)
        {
            writer.Write("sortAfter");
            WriteList(SortAfter, writer);
        }

        if (SortBefore.Length > 0)
        {
            writer.Write("sortBefore");
            WriteList(SortBefore, writer);
        }

        if (Author.Length > 0)
        {
            writer.Write("author");
            writer.Write(Author);
        }

        writer.Write("version");
        writer.Write(Version.ToString());
        if (DisplayName.Length > 0)
        {
            writer.Write("displayName");
            writer.Write(DisplayName);
        }

        if (Homepage.Length > 0)
        {
            writer.Write("homepage");
            writer.Write(Homepage);
        }

        if (Description.Length > 0)
        {
            writer.Write("description");
            writer.Write(Description);
        }

        if (!PlayableOnPreview)
        {
            writer.Write("!playableOnPreview");
        }

        if (TranslationMod)
        {
            writer.Write("translationMod");
        }

        if (!HideCode)
        {
            writer.Write("!hideCode");
        }

        if (!HideResources)
        {
            writer.Write("!hideResources");
        }

        if (IncludeSource)
        {
            writer.Write("includeSource");
        }

        if (EacPath.Length > 0)
        {
            writer.Write("eacPath");
            writer.Write(EacPath);
        }

        if (Side != ModSide.Both)
        {
            writer.Write("side");
            writer.Write((byte)Side);
        }

        if (ModSource.Length > 0)
        {
            writer.Write("modSource");
            writer.Write(ModSource);
        }

        writer.Write("buildVersion");
        writer.Write(buildVersion);

        writer.Write("");

        return memoryStream.ToArray();

        static void WriteList<T>(IEnumerable<T> list, BinaryWriter writer)
        {
            foreach (var item in list)
            {
                writer.Write(item!.ToString()!);
            }

            writer.Write("");
        }
    }
}
