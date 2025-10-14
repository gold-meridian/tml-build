using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Tomat.Files.Tmod;

partial class BuildProperties
{
    public static BuildProperties ReadBuildInfo(string buildFile)
    {
        var properties = new BuildProperties();

        foreach (var line in File.ReadAllLines(buildFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var split = line.IndexOf('=');
            var property = line.Substring(0, split).Trim();
            var value = line.Substring(split + 1).Trim();
            if (value.Length == 0)
            {
                continue;
            }

            ProcessProperty(properties, property, value);
        }

        VerifyRefs(properties.GetReferenceNames(true).ToList());
        properties.SortAfter = GetDistinctRefs();

        return properties;

        static void VerifyRefs(List<string> refs)
        {
            if (refs.Distinct().Count() != refs.Count)
            {
                throw new DuplicateNameException("Duplicate mod or weak references.");
            }
        }

        // Adds (mod|weak)References that are not in sortBefore to sortAfter
        string[] GetDistinctRefs()
        {
            return properties.GetReferenceNames(true)
                             .Where(dep => !properties.SortBefore.Contains(dep))
                             .Concat(properties.SortAfter).Distinct().ToArray();
        }
    }

    private static void ProcessProperty(BuildProperties properties, string property, string value)
    {
        switch (property)
        {
            case "dllReferences":
                properties.DllReferences = ReadList(value).ToList();
                break;

            case "modReferences":
                properties.ModReferences = ReadList(value).Select(ModReference.Parse).ToList();
                break;

            case "weakReferences":
                properties.WeakReferences = ReadList(value).Select(ModReference.Parse).ToList();
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
                properties.Version = new Version(value);
                break;

            case "displayName":
                properties.DisplayName = value;
                break;

            case "homepage":
                properties.Homepage = value;
                break;

            case "noCompile":
                properties.NoCompile = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "playableOnPreview":
                properties.PlayableOnPreview = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "translationMod":
                properties.TranslationMod = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "hideCode":
                properties.HideCode = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "hideResources":
                properties.HideResources = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "includeSource":
                properties.IncludeSource = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                break;

            case "buildIgnore":
                properties.BuildIgnores = value.Split(',')
                                               .Select(s => s.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
                                               .Where(s => s.Length > 0)
                                               .ToArray();
                break;

            case "side":
                if (!Enum.TryParse<ModSide>(value, true, out var side))
                {
                    throw new ArgumentException("Side is not one of (Both, Client, Server, NoSync): " + value);
                }

                properties.Side = side;
                break;
        }

        return;

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

        if (NoCompile)
        {
            writer.Write("noCompile");
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
