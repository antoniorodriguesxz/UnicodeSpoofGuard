using System.Text;
using UnicodeSpoofGuard;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Detection;
using UnicodeSpoofGuard.Structures;
using Xunit;

namespace UnicodeSpoofGuard.Tests;

public class UnicodeSpoofGuardTests
{
    private readonly UnicodeSpoofGuard _guard = new();

    [Fact]
    public void IsSafeEmail_ShouldReturnFalseForSpoofedEmail()
    {
        var spoofed = "test@p" + '\u0430' + "ypal.com";
        var result = _guard.IsSafeEmail(spoofed);
        Assert.False(result.IsSafe);
        Assert.NotEmpty(result.Reason);
        Assert.Equal("test@paypal.com", result.CanonicalValue);
    }

    [Fact]
    public void IsSafeEmail_ShouldReturnTrueForCleanEmail()
    {
        var result = _guard.IsSafeEmail("test@paypal.com");
        Assert.True(result.IsSafe, string.Join(" | ", result.Findings.Select(f => $"{f.FindingType}:{f.Description}")));
        Assert.Empty(result.Reason);
    }

    [Fact]
    public void GetCanonicalString_ShouldReturnCanonicalRepresentation()
    {
        var spoofed = "p" + '\u0430' + "ypal";
        var result = _guard.GetCanonicalString(spoofed);
        Assert.Equal("paypal", result);
    }

    [Fact]
    public void AnalyzeTextForSpoofing_ShouldReturnDetailedFindings()
    {
        var spoofed = "p" + '\u0430' + "ypal";
        var result = _guard.AnalyzeTextForSpoofing(spoofed);
        Assert.NotEmpty(result);
        Assert.Contains(result, f => f.FindingType == "Homoglyph");
    }

    [Fact]
    public void IsSafeDomain_ShouldRespectThreatIntelOption()
    {
        var options = new AnalysisOptions { DisableThreatIntel = true };
        var result = _guard.IsSafeDomain("paypal-security-center.com", options);

        Assert.True(result.IsSafe);
        Assert.DoesNotContain(result.Findings, f => f.FindingType == "ThreatIntel");
    }

    [Fact]
    public void IsSafeUsername_StrictModeSurfacesAsciiHomoglyphs()
    {
        var options = new AnalysisOptions { UseStrictMode = true };
        var result = _guard.IsSafeUsername("paypa1", options);

        Assert.False(result.IsSafe);
        Assert.Contains(result.Findings, f => f.Substring == "1");
    }

    [Fact]
    public void RefreshConfusablesData_ShouldOverrideDatasetFromBinary()
    {
        var original = ConfusablesDatasetLoader.Dataset;

        const string sample =
@"# confusables.txt
# Date: 2025-07-22, 05:49:37 GMT
# Version: TestRuntime
0041 ; 0042 ; MA    # ( A → B ) LATIN CAPITAL LETTER A → LATIN CAPITAL LETTER B    #
";

        try
        {
            var payload = Encoding.UTF8.GetBytes(sample);
            var refresh = _guard.RefreshConfusablesData(ConfusablesContent: payload);

            Assert.True(refresh.IsSuccess, refresh.Message);
            Assert.Equal("TestRuntime", refresh.SourceVersion);

            var canonical = _guard.GetCanonicalString("A");
            Assert.Equal("B", canonical);
        }
        finally
        {
            ConfusablesDatasetLoader.SetDataset(original);
            ConfusablesIndex.Reload(original);
        }
    }
}
