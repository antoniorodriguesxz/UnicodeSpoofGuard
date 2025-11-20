using System.Collections.Generic;
using System.Text;
using UnicodeSpoofGuard.Data;

namespace UnicodeSpoofGuard.Feeds;

internal sealed class ThreatIndicatorStore
{
    private static readonly Lazy<ThreatIndicatorStore> Cached = new(() => new ThreatIndicatorStore(ThreatIntelLoader.Dataset), isThreadSafe: true);

    public static ThreatIndicatorStore Instance => Cached.Value;

    private readonly Dictionary<string, ThreatIndicator> _lookup;

    private ThreatIndicatorStore(ThreatFeedDataset dataset)
    {
        _lookup = new Dictionary<string, ThreatIndicator>(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in dataset.Feeds)
        {
            if (feed.Indicators is null || feed.Indicators.Count == 0)
            {
                continue;
            }

            foreach (var record in feed.Indicators)
            {
                if (string.IsNullOrWhiteSpace(record.Value))
                {
                    continue;
                }

                var expiresAt = record.ExpiresAt;
                if (!expiresAt.HasValue && feed.TtlMinutes.HasValue && feed.TtlMinutes.Value > 0)
                {
                    expiresAt = feed.RetrievedAt.AddMinutes(feed.TtlMinutes.Value);
                }

                if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
                {
                    continue;
                }

                var indicator = new ThreatIndicator(
                    record.Value,
                    string.IsNullOrWhiteSpace(record.Type) ? "generic" : record.Type,
                    string.IsNullOrWhiteSpace(record.Source) ? feed.Id : record.Source,
                    expiresAt);

                _lookup[indicator.Value.ToLowerInvariant()] = indicator;
            }
        }
    }

    public IReadOnlyCollection<ThreatIndicator> Indicators => _lookup.Values;

    public bool TryMatch(string canonicalValue, out ThreatIndicator indicator, out string matchedToken)
    {
        foreach (var match in FindMatches(canonicalValue))
        {
            matchedToken = match.Token;
            indicator = match.Indicator;
            return true;
        }

        indicator = default;
        matchedToken = string.Empty;
        return false;
    }

    public IEnumerable<(string Token, ThreatIndicator Indicator)> FindMatches(string canonicalValue)
    {
        if (string.IsNullOrWhiteSpace(canonicalValue))
        {
            yield break;
        }

        var normalized = canonicalValue.ToLowerInvariant();

        if (_lookup.TryGetValue(normalized, out var indicator))
        {
            yield return (normalized, indicator);
        }

        foreach (var token in Tokenize(normalized))
        {
            if (_lookup.TryGetValue(token, out indicator))
            {
                yield return (token, indicator);
            }
        }
    }

    private static IEnumerable<string> Tokenize(string canonical)
    {
        var builder = new StringBuilder();

        foreach (var ch in canonical)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '.' or '@')
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}

internal readonly record struct ThreatIndicator(
    string Value,
    string Type,
    string Source,
    DateTimeOffset? ExpiresAt);

