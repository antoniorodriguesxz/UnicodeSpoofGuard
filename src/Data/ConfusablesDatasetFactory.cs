using System;
using System.Globalization;
using System.IO;
using System.Linq;
namespace UnicodeSpoofGuard.Data;

internal static class ConfusablesDatasetFactory
{
    private const string VersionPrefix = "Version:";
    private const string DatePrefix = "Date:";
    private const string HashPrefix = "Hash:";

    public static ConfusablesDataset FromText(string content, string? sourceUri)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Confusables content cannot be empty.", nameof(content));
        }

        var metadata = ParseMetadata(content, sourceUri);
        var entries = ConfusablesTextParser.Parse(content);

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Confusables content did not yield any entries.");
        }

        return new ConfusablesDataset(
            metadata.Version,
            metadata.SourceDate,
            metadata.RetrievedAt,
            metadata.SourceUri,
            metadata.Hash,
            entries);
    }

    private static ConfusablesMetadata ParseMetadata(string content, string? sourceUri)
    {
        string version = "unknown";
        DateTimeOffset? sourceDate = null;
        string? hash = null;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.StartsWith("#", StringComparison.Ordinal))
            {
                break;
            }

            var payload = line[1..].Trim();
            if (payload.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                version = payload[VersionPrefix.Length..].Trim();
            }
            else if (payload.StartsWith(DatePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var dateText = payload[DatePrefix.Length..].Trim();
                if (DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    sourceDate = parsed;
                }
            }
            else if (payload.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hash = payload[HashPrefix.Length..].Trim();
            }
        }

        return new ConfusablesMetadata(
            version,
            sourceDate ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            sourceUri,
            hash);
    }

    private sealed record ConfusablesMetadata(
        string Version,
        DateTimeOffset SourceDate,
        DateTimeOffset RetrievedAt,
        string? SourceUri,
        string? Hash);
}

