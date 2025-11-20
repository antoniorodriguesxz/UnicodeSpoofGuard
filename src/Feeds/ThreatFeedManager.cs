using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnicodeSpoofGuard.Data;

namespace UnicodeSpoofGuard.Feeds;

internal interface IThreatFeedClient
{
    Task<string> FetchAsync(Uri uri, CancellationToken cancellationToken);
}

internal sealed class HttpThreatFeedClient(HttpClient httpClient) : IThreatFeedClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<string> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class FileThreatFeedClient : IThreatFeedClient
{
    public async Task<string> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.IsFile)
        {
            throw new InvalidOperationException($"URI '{uri}' is not a file path.");
        }

        await using var stream = new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ThreatFeedManager : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IThreatFeedClient _httpClientAdapter;
    private readonly IThreatFeedClient _fileClient = new FileThreatFeedClient();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _baseDirectory;
    private readonly bool _ownsHttpClient;

    public ThreatFeedManager(HttpClient? httpClient = null, string? baseDirectory = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _httpClientAdapter = new HttpThreatFeedClient(_httpClient);
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public async Task<ThreatFeedSnapshot> FetchAsync(ThreatFeedDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!definition.Enabled)
        {
            return ThreatFeedSnapshot.Disabled(definition.Id);
        }

        if (_cache.TryGetValue(definition.Id, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Snapshot;
        }

        var uri = definition.ResolveUri(_baseDirectory);
        var rawPayload = await RetrieveAsync(definition.Type, uri, cancellationToken).ConfigureAwait(false);
        var values = ThreatFeedParser.Parse(rawPayload, definition.Format);

        var retrievedAt = DateTimeOffset.UtcNow;
        var ttlMinutes = Math.Max(definition.TtlMinutes, 1);
        var indicators = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new ThreatIndicator(
                value.Trim(),
                definition.IndicatorType,
                definition.Id,
                retrievedAt.AddMinutes(ttlMinutes)))
            .ToList();

        var snapshot = new ThreatFeedSnapshot(definition.Id, definition.Description, retrievedAt, ttlMinutes, indicators);
        var expiry = retrievedAt.AddMinutes(ttlMinutes);
        _cache[definition.Id] = new CacheEntry(snapshot, expiry);

        return snapshot;
    }

    public async Task<ThreatFeedDataset> BuildDatasetAsync(ThreatFeedConfig config, CancellationToken cancellationToken = default)
    {
        var feeds = new List<ThreatFeedEntry>();

        foreach (var definition in config.Feeds)
        {
            if (!definition.Enabled)
            {
                continue;
            }

            var snapshot = await FetchAsync(definition, cancellationToken).ConfigureAwait(false);
            var indicatorRecords = snapshot.Indicators
                .Select(indicator => new ThreatIndicatorRecord(indicator.Value, indicator.Type, indicator.Source, indicator.ExpiresAt))
                .ToList();

            feeds.Add(new ThreatFeedEntry(definition.Id, snapshot.RetrievedAt, snapshot.TtlMinutes, indicatorRecords));
        }

        return new ThreatFeedDataset(DateTimeOffset.UtcNow, feeds);
    }

    private async Task<string> RetrieveAsync(string type, Uri uri, CancellationToken cancellationToken)
    {
        return type.ToLowerInvariant() switch
        {
            "http" or "https" => await _httpClientAdapter.FetchAsync(uri, cancellationToken).ConfigureAwait(false),
            "file" => await _fileClient.FetchAsync(uri, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported feed type '{type}' in threat feed configuration.")
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private sealed record CacheEntry(ThreatFeedSnapshot Snapshot, DateTimeOffset ExpiresAt);
}

internal sealed record ThreatFeedSnapshot(
    string Id,
    string? Description,
    DateTimeOffset RetrievedAt,
    int TtlMinutes,
    IReadOnlyList<ThreatIndicator> Indicators)
{
    public static ThreatFeedSnapshot Disabled(string id) =>
        new(id, "Feed disabled", DateTimeOffset.UtcNow, 0, Array.Empty<ThreatIndicator>());
}

internal static class ThreatFeedParser
{
    public static IReadOnlyList<string> Parse(string payload, string format)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<string>();
        }

        return format.ToLowerInvariant() switch
        {
            "json-array" => ParseJsonArray(payload),
            "plain-text" => ParsePlainText(payload),
            _ => throw new InvalidOperationException($"Unsupported threat feed format '{format}'.")
        };
    }

    private static IReadOnlyList<string> ParseJsonArray(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Threat feed JSON payload must be an array.");
        }

        var results = new List<string>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("indicator", out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value!);
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<string> ParsePlainText(string payload)
    {
        var results = new List<string>();
        using var reader = new StringReader(payload);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(trimmed);
        }

        return results;
    }
}

