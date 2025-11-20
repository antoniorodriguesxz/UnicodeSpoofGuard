using System.Linq;
using UnicodeSpoofGuard;
using UnicodeSpoofGuard.Detection;
using Xunit;

namespace UnicodeSpoofGuard.Tests;

public class PolicyTests
{
    [Fact]
    public void MixedScriptFlaggedWhenNoPolicyAllowsCombination()
    {
        var detector = new SpoofDetector();
        var findings = detector.Analyze("ΑBC"); // Greek Alpha + Latin letters

        Assert.Contains(findings, f => f.FindingType == "MixedScript");
    }

    [Fact]
    public void LocaleAllowsGreekLatinMix()
    {
        var options = new SpoofGuardOptions { Locale = "el-GR" };
        var detector = new SpoofDetector(options);

        var findings = detector.Analyze("ΑBC");

        Assert.DoesNotContain(findings, f => f.FindingType == "MixedScript");
        Assert.Contains(findings, f => f.FindingType == "Homoglyph");
    }

    [Fact]
    public void ExplicitPolicyProfileAllowsCyrillicLatinMix()
    {
        var options = new SpoofGuardOptions { PolicyProfile = "latin-cyrillic" };
        var detector = new SpoofDetector(options);

        var findings = detector.Analyze("pаypal"); // Cyrillic a in Latin word

        Assert.DoesNotContain(findings, f => f.FindingType == "MixedScript");
        Assert.Contains(findings, f => f.FindingType == "Homoglyph");
    }
}

