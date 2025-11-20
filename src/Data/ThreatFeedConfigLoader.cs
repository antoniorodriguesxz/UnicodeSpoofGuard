using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnicodeSpoofGuard.Data;

internal static class ThreatFeedConfigLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.config.threat-feeds.json";
    private static readonly Lazy<ThreatFeedConfig> Cached = new(Load, isThreadSafe: true);

    public static ThreatFeedConfig Config => Cached.Value;

    private static ThreatFeedConfig Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");

        var config = JsonSerializer.Deserialize<ThreatFeedConfig>(stream, JsonOptions);
        return config ?? new ThreatFeedConfig(new List<ThreatFeedDefinition>());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed record ThreatFeedConfig(
    [property: JsonPropertyName("feeds")] IReadOnlyList<ThreatFeedDefinition> Feeds);

internal sealed record ThreatFeedDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("indicatorType")] string IndicatorType,
    [property: JsonPropertyName("ttlMinutes")] int TtlMinutes,
    [property: JsonPropertyName("enabled")] bool Enabled = true)
{
    public Uri ResolveUri(string? baseDirectory = null)
    {
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var absolute))
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new InvalidOperationException($"Feed '{Id}' has relative URI '{Uri}' but no base path was provided.");
            }

            var combined = Path.Combine(baseDirectory, Uri)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return new Uri(Path.GetFullPath(combined));
        }

        return absolute;
    }
}

