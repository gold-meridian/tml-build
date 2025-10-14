using System.IO;
using NUnit.Framework;
using Tomat.Files.Tmod;

namespace Tomat.Files.Tests;

[TestFixture]
public static class TmodTests
{
    [TestCase("CalamityMod.tmod")]
    [TestCase("LiquidSlopesPatch.tmod")]
    [TestCase("SonarIcons.tmod")]
    [TestCase("ThoriumMod.tmod")]
    public static void VerifyReadAndWriteIdenticalData(string filePath)
    {
        var fileBytes = File.ReadAllBytes(filePath);

        var readOnlyTmod = TmodFileSerializer.Read(new MemoryStream(fileBytes));
        var tmod = readOnlyTmod.AsMutable();

        using var dest = new MemoryStream();
        TmodFileSerializer.Write(dest, tmod);

        Assert.That(dest.ToArray(), Is.EquivalentTo(fileBytes));
    }
}
