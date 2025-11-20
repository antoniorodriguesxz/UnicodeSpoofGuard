using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace UnicodeSpoofGuard.Data;

internal static class ConfusablesDatasetLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.data.confusables.json";
    private const string BinaryResourceName = "UnicodeSpoofGuard.data.confusables.bin";
    private static readonly object SyncRoot = new();
    private static ConfusablesDataset? _cached;

    public static ConfusablesDataset Dataset
    {
        get
        {
            var snapshot = Volatile.Read(ref _cached);
            if (snapshot is not null)
            {
                return snapshot;
            }

            lock (SyncRoot)
            {
                snapshot = Volatile.Read(ref _cached);
                if (snapshot is null)
                {
                    snapshot = Load();
                    Volatile.Write(ref _cached, snapshot);
                }
                return snapshot;
            }
        }
    }

    internal static void SetDataset(ConfusablesDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        lock (SyncRoot)
        {
            Volatile.Write(ref _cached, dataset);
        }
    }

    private static ConfusablesDataset Load()
    {
        if (TryLoadFromBinary(out var dataset))
        {
            return dataset;
        }

        return LoadFromJsonResource();
    }

    private static bool TryLoadFromBinary(out ConfusablesDataset dataset)
    {
        try
        {
            var handle = BinaryResourceLoader.MapEmbeddedResource(BinaryResourceName, "confusables.bin");
            using var view = handle.CreateViewStream();
            using var gzip = new GZipStream(view, CompressionMode.Decompress, leaveOpen: false);
            dataset = JsonSerializer.Deserialize<ConfusablesDataset>(gzip, JsonOptions)
                      ?? throw new InvalidOperationException("Unable to deserialize confusables dataset from binary cache.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or FileNotFoundException)
        {
        }

        dataset = default!;
        return false;
    }

    private static ConfusablesDataset LoadFromJsonResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");
        var dataset = JsonSerializer.Deserialize<ConfusablesDataset>(stream, JsonOptions)
                      ?? throw new InvalidOperationException("Unable to deserialize confusables dataset.");
        return dataset;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed record ConfusablesDataset(
    string SourceVersion,
    DateTimeOffset SourceDate,
    DateTimeOffset RetrievedAt,
    string? SourceUri,
    string? HashSha512,
    List<ConfusableEntry> Entries);

internal sealed record ConfusableEntry(
    [property: JsonPropertyName("Source")] string Source,
    [property: JsonPropertyName("Target")] string Target,
    [property: JsonPropertyName("Type")] string? Type,
    [property: JsonPropertyName("Comment")] string? Comment);

