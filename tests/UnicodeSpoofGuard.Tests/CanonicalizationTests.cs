using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnicodeSpoofGuard;
using Xunit;

namespace UnicodeSpoofGuard.Tests;

public class CanonicalizationTests
{
    private static readonly CanonicalizationFixture Fixture = CanonicalizationFixture.Load();

    public static IEnumerable<object[]> MultiCharCases() =>
        Fixture.CanonicalizationCases.Select(c => new object[] { c.Description, c.Input, c.Expected });

    [Theory]
    [MemberData(nameof(MultiCharCases))]
    public void ToCanonical_NormalizesSpoofSequences(string description, string input, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        var canonical = Canonicalizer.ToCanonical(input);
        Assert.Equal(expected, canonical);
    }
}

internal sealed class CanonicalizationFixture
{
    public IReadOnlyList<CanonicalizationCase> CanonicalizationCases { get; init; } = Array.Empty<CanonicalizationCase>();

    public static CanonicalizationFixture Load()
    {
        var basePath = AppContext.BaseDirectory;
        var fixturePath = Path.Combine(basePath, "Fixtures", "spoof_cases.json");
        var json = File.ReadAllText(fixturePath);
        return JsonSerializer.Deserialize<CanonicalizationFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new CanonicalizationFixture();
    }
}

internal sealed record CanonicalizationCase(string Description, string Input, string Expected);

