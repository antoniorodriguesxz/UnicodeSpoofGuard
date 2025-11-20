using System.Collections.Generic;
using System.Linq;
using UnicodeSpoofGuard;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Detection;
using Xunit;

namespace UnicodeSpoofGuard.Tests;

public class HeuristicsTests
{
    private static readonly ConfusableMapping AsciiPair = new("1", "l", "MA", "ASCII lookalike");
    private static readonly ConfusableMapping IdenticalPair = new("A", "A", "MA", "Identical mapping");
    private static readonly ConfusableMapping GreekToLatin = new("\u0391", "A", "MA", "Greek alpha to Latin A");

    private static ConfusablesIndex BuildIndex() =>
        ConfusablesIndex.CreateForTesting(new[] { AsciiPair, IdenticalPair, GreekToLatin });

    private static PolicyConfig BuildPolicyConfig()
    {
        var profile = new PolicyProfile
        {
            AllowedMixedScripts = new List<List<string>>()
        };
        profile.Initialize();

        return new PolicyConfig
        {
            DefaultProfile = "test",
            Profiles = new Dictionary<string, PolicyProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["test"] = profile
            }
        };
    }

    private static DetectionConfig BuildConfig()
    {
        var balanced = new DetectionProfile
        {
            SuppressAsciiConfusables = true,
            SuppressIdenticalMappings = true,
            ConfusableClassThresholds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = 0.5,
                ["MA"] = 0.5
            }
        };

        var strict = new DetectionProfile
        {
            SuppressAsciiConfusables = false,
            SuppressIdenticalMappings = false,
            ConfusableClassThresholds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = 0.2,
                ["MA"] = 0.2
            }
        };

        return new DetectionConfig
        {
            DefaultMode = "balanced",
            Modes = new Dictionary<string, DetectionProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["balanced"] = balanced,
                ["strict"] = strict
            }
        };
    }

    [Fact]
    public void BalancedMode_SuppressesAsciiAndIdenticalMappings()
    {
        var detector = new SpoofDetector(BuildIndex(), BuildConfig(), BuildPolicyConfig(), SpoofGuardOptions.Default);

        var input = $"\u03911A"; // Greek Alpha, ASCII digit 1, ASCII letter A
        var findings = detector.Analyze(input);

        var homoglyphs = findings.Where(f => f.FindingType == "Homoglyph").ToList();

        Assert.Single(homoglyphs);
        Assert.Equal("\u0391", homoglyphs[0].Substring);
        Assert.DoesNotContain(homoglyphs, f => f.Substring == "1");
        Assert.DoesNotContain(homoglyphs, f => f.Substring == "A");
    }

    [Fact]
    public void StrictMode_IncludesAsciiAndIdenticalMappings()
    {
        var detector = new SpoofDetector(BuildIndex(), BuildConfig(), BuildPolicyConfig(), new SpoofGuardOptions { StrictMode = true });

        var input = $"\u03911A";
        var findings = detector.Analyze(input);

        var homoglyphs = findings.Where(f => f.FindingType == "Homoglyph").ToList();

        Assert.Equal(3, homoglyphs.Count);
        Assert.Contains(homoglyphs, f => f.Substring == "\u0391");
        Assert.Contains(homoglyphs, f => f.Substring == "1");
        Assert.Contains(homoglyphs, f => f.Substring == "A");
    }
}

