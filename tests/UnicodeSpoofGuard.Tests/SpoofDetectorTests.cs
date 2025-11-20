using UnicodeSpoofGuard;
using UnicodeSpoofGuard.Detection;
using Xunit;

namespace UnicodeSpoofGuard.Tests;

public class SpoofDetectorTests
{
    private readonly SpoofDetector _detector = new();

    [Theory]
    [InlineData("pаypal", "Homoglyph")] // Cyrillic 'а'
    [InlineData("microsоft", "Homoglyph")] // Cyrillic 'о'
    public void Analyze_ShouldDetectHomoglyphs(string input, string findingType)
    {
        var findings = _detector.Analyze(input);
        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.FindingType == findingType);
    }

    [Theory]
    [InlineData("привет world", "MixedScript")] // Cyrillic and Latin
    public void Analyze_ShouldDetectMixedScripts(string input, string findingType)
    {
        var findings = _detector.Analyze(input);
        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.FindingType == findingType);
    }

    [Theory]
    [InlineData("text\u200Bwith\u200Cinvisible\u200Dchars", "InvisibleCharacter")] // Zero-width space, etc.
    public void Analyze_ShouldDetectInvisibleCharacters(string input, string findingType)
    {
        var findings = _detector.Analyze(input);
        Assert.True(findings.Count > 0);
        Assert.Contains(findings, f => f.FindingType == findingType);
    }

    [Theory]
    [InlineData("user\u202Etxt.exe", "BidirectionalControl")] // Right-to-Left Override
    public void Analyze_ShouldDetectBidirectionalControlCharacters(string input, string findingType)
    {
        var findings = _detector.Analyze(input);
        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.FindingType == findingType);
    }

    [Fact]
    public void Analyze_ShouldReturnEmptyListForCleanText()
    {
        var findings = _detector.Analyze("clean text");
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_ShouldDetectThreatIntelIndicators()
    {
        var detector = new SpoofDetector(new SpoofGuardOptions { StrictMode = true });
        var findings = detector.Analyze("Please visit paypal-security-center.com for details.");

        Assert.Contains(findings, f => f.FindingType == "ThreatIntel");
    }

    [Fact]
    public void Analyze_ShouldSkipThreatIntelWhenDisabled()
    {
        var detector = new SpoofDetector(new SpoofGuardOptions { EnableThreatIntel = false });
        var findings = detector.Analyze("Please visit paypal-security-center.com for details.");

        Assert.DoesNotContain(findings, f => f.FindingType == "ThreatIntel");
    }
}
