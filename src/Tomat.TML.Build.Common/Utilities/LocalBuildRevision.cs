using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.Common.Utilities;

public enum BuildPurpose
{
    Stable,
    Preview,
    Dev,
}

public sealed record LocalBuildRevision(
    Version TmlVersion,
    Version StableVersion,
    string BranchName,
    BuildPurpose BuildPurpose,
    string CommitSha,
    DateTime BuildDate
)
{
    public static bool TryGetFromModule(
        ILogWrapper log,
        string tmlDllPath,
        [NotNullWhen(returnValue: true)] out LocalBuildRevision? revision
    )
    {
        revision = null;

        var versionData = GetAssemblyInformationalVersionData(tmlDllPath);
        if (versionData is null)
        {
            log.Error("Failed to get assembly informational version data from the tModLoader assembly");
            return false;
        }

        try
        {
            var parts = versionData[(versionData.IndexOf('+') + 1)..].Split('|');
            var tmlVersion = new Version(parts[0]);
            var stableVersion = new Version(parts[1]);
            var branchName = parts[2];
            var buildPurpose = (BuildPurpose)Enum.Parse(typeof(BuildPurpose), parts[3], ignoreCase: true);
            var commitSha = parts[4];
            var buildDate = DateTime.FromBinary(long.Parse(parts[5]));

            revision = new LocalBuildRevision(
                tmlVersion,
                stableVersion,
                branchName,
                buildPurpose,
                commitSha,
                buildDate
            );
            return true;
        }
        catch (Exception e)
        {
            log.Error($"Failed to parse assembly informational version data: {e}");
            return false;
        }
    }

    private static string? GetAssemblyInformationalVersionData(string dllPath)
    {
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);

        var mdReader = peReader.GetMetadataReader();

        foreach (var handle in mdReader.CustomAttributes)
        {
            var attr = mdReader.GetCustomAttribute(handle);

            var ctor = attr.Constructor;

            StringHandle nameHandle;
            if (ctor.Kind == HandleKind.MemberReference)
            {
                var memberRef = mdReader.GetMemberReference((MemberReferenceHandle)ctor);
                var container = memberRef.Parent;

                if (container.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var typeRef = mdReader.GetTypeReference((TypeReferenceHandle)container);
                nameHandle = typeRef.Name;
            }
            else
            {
                continue;
            }

            var attrTypeName = mdReader.GetString(nameHandle);
            if (attrTypeName != "AssemblyInformationalVersionAttribute")
            {
                continue;
            }

            // Decode the blob (fixed prolog + single string argument)
            var valueReader = mdReader.GetBlobReader(attr.Value);

            // Skip prolog (0x0001)
            valueReader.ReadUInt16();

            return valueReader.ReadSerializedString();
        }

        return null;
    }
}
