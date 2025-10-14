using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using Tomat.Files.Tmod;

namespace Tomat.Files.Tests;

[TestFixture]
public static class TmodTests
{
    [TestCase("SonarIcons.tmod")]
    [TestCase("LiquidSlopesPatch.tmod")]
    [TestCase("CalamityMod.tmod")]
    [TestCase("ThoriumMod.tmod")]
    public static void VerifyReadAndWriteIdenticalData(string filePath)
    {
        TestContext.Out.WriteLine("Reading file");
        var fileBytes = File.ReadAllBytes(filePath);

        TestContext.Out.WriteLine("Reading TMOD");
        var readOnlyTmod = TmodFileSerializer.Read(new MemoryStream(fileBytes));

        TestContext.Out.WriteLine("Copying mutable");
        var tmod = readOnlyTmod.AsMutable();

        TestContext.Out.WriteLine("Writing TMOD");
        using var dest = new MemoryStream();
        TmodFileSerializer.Write(dest, tmod);

        TestContext.Out.WriteLine("Checking equality");
        File.WriteAllBytes(filePath + "2", dest.ToArray());
        Assert.That(SHA1.HashData(dest.ToArray()), Is.EquivalentTo(SHA1.HashData(fileBytes)));
    }
}
