using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnicodeSpoofGuard.Data;

internal static class ThreatIntelLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.data.threat_indicators.json";
    private static readonly Lazy<ThreatFeedDataset> Cached = new(Load, isThreadSafe: true);

    public static ThreatFeedDataset Dataset => Cached.Value;

    private static ThreatFeedDataset Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");

        var dataset = JsonSerializer.Deserialize<ThreatFeedDataset>(stream, JsonOptions);
        return dataset ?? new ThreatFeedDataset(DateTimeOffset.MinValue, new List<ThreatFeedEntry>());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed record ThreatFeedDataset(
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("feeds")] IReadOnlyList<ThreatFeedEntry> Feeds);

internal sealed record ThreatFeedEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("retrievedAt")] DateTimeOffset RetrievedAt,
    [property: JsonPropertyName("ttlMinutes")] int? TtlMinutes,
    [property: JsonPropertyName("indicators")] IReadOnlyList<ThreatIndicatorRecord> Indicators);

internal sealed record ThreatIndicatorRecord(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt);

