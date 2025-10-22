using NUnit.Framework;
using Tomat.Parsing.Diagnostics;

namespace Tomat.TML.Build.Tests.Diagnostics;

[TestFixture]
public static class ErrorMessageParsingTests
{
    [TestCase("C:\\Users\\sgt\\Documents\\My Games\\Terraria\\tModLoader\\ModSources\\nightshade-mod\\src\\Nightshade\\Assets\\Shaders\\UI\\ModPanelShader.hlsl(44,35-48): warning X3571: pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them")]
    public static void EnsureParses(string message)
    {
        var error = CanonicalError.Parse(message);
        Assert.That(error, Is.Not.Null);
    }
}
