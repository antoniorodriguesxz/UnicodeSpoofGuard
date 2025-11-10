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
}
