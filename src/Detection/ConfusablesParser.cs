using System.Threading;
using UnicodeSpoofGuard.Data;

namespace UnicodeSpoofGuard.Detection;

internal sealed class ConfusablesIndex
{
    private static readonly object SyncRoot = new();
    private static ConfusablesIndex? _instance;

    public static ConfusablesIndex Instance
    {
        get
        {
            var snapshot = Volatile.Read(ref _instance);
            if (snapshot is not null)
            {
                return snapshot;
            }

            lock (SyncRoot)
            {
                snapshot = Volatile.Read(ref _instance);
                if (snapshot is null)
                {
                    snapshot = Build(ConfusablesDatasetLoader.Dataset);
                    Volatile.Write(ref _instance, snapshot);
                }
                return snapshot;
            }
        }
    }

    private readonly Dictionary<string, ConfusableMapping> _singleCharMap;
    private readonly Dictionary<int, Dictionary<string, ConfusableMapping>> _multiCharMap;
    private readonly Dictionary<string, string> _singleCharCanonicalMap;
    private readonly Dictionary<string, string> _asciiCanonicalMap;
    private readonly int _maxSequenceLength;

    private ConfusablesIndex(
        Dictionary<string, ConfusableMapping> singleCharMap,
        Dictionary<int, Dictionary<string, ConfusableMapping>> multiCharMap,
        Dictionary<string, string> singleCharCanonicalMap,
        Dictionary<string, string> asciiCanonicalMap,
        int maxSequenceLength)
    {
        _singleCharMap = singleCharMap;
        _multiCharMap = multiCharMap;
        _singleCharCanonicalMap = singleCharCanonicalMap;
        _asciiCanonicalMap = asciiCanonicalMap;
        _maxSequenceLength = maxSequenceLength;
    }

    public bool TryGetSingleCharMapping(string key, out ConfusableMapping mapping) =>
        _singleCharMap.TryGetValue(key, out mapping!);

    public IReadOnlyDictionary<string, string> GetSingleCharCanonicalMap() => _singleCharCanonicalMap;

    public IReadOnlyDictionary<string, string> GetAsciiCanonicalMap() => _asciiCanonicalMap;

    public IReadOnlyDictionary<string, ConfusableMapping> GetMultiCharMappings(int length) =>
        _multiCharMap.TryGetValue(length, out var map) ? map : ReadOnlyDictionaryEmpty<string, ConfusableMapping>.Value;

    public int MaxSequenceLength => _maxSequenceLength;

    internal static ConfusablesIndex CreateForTesting(IEnumerable<ConfusableMapping> entries)
    {
        var singleCharMap = new Dictionary<string, ConfusableMapping>(StringComparer.Ordinal);
        var multiCharMap = new Dictionary<int, Dictionary<string, ConfusableMapping>>();
        var singleCharCanonicalMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var asciiCanonicalMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int maxSequenceLength = 1;

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Source) || string.IsNullOrEmpty(entry.Target))
            {
                continue;
            }

            if (entry.Source.Length == 1)
            {
                singleCharMap[entry.Source] = entry;

                if (entry.Target.Length == 1)
                {
                    singleCharCanonicalMap[entry.Source] = entry.Target;
                }
            }
            else
            {
                if (!multiCharMap.TryGetValue(entry.Source.Length, out var map))
                {
                    map = new Dictionary<string, ConfusableMapping>(StringComparer.Ordinal);
                    multiCharMap[entry.Source.Length] = map;
                }
                map[entry.Source] = entry;

                if (entry.Source.Length > maxSequenceLength)
                {
                    maxSequenceLength = entry.Source.Length;
                }
            }

            if (entry.IsAsciiPair)
            {
                asciiCanonicalMap[entry.Source] = entry.Target;
            }
        }

        return new ConfusablesIndex(singleCharMap, multiCharMap, singleCharCanonicalMap, asciiCanonicalMap, maxSequenceLength);
    }

    internal static void Reload(ConfusablesDataset dataset)
    {
        var rebuilt = Build(dataset);
        lock (SyncRoot)
        {
            Volatile.Write(ref _instance, rebuilt);
        }
    }

    private static ConfusablesIndex Build(ConfusablesDataset dataset)
    {
        var map = new Dictionary<string, ConfusableMapping>(StringComparer.Ordinal);
        var multi = new Dictionary<int, Dictionary<string, ConfusableMapping>>();
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var asciiCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int maxSequenceLength = 1;

        foreach (var entry in dataset.Entries)
        {
            if (string.IsNullOrEmpty(entry.Source) || string.IsNullOrEmpty(entry.Target))
            {
                continue;
            }

            // Track single-character source entries for fast lookup during detection.
            var mapping = new ConfusableMapping(entry.Source, entry.Target, entry.Type, entry.Comment);

            if (mapping.SourceLength == 1)
            {
                map[entry.Source] = mapping;
            }
            else
            {
                if (!multi.TryGetValue(mapping.SourceLength, out var dict))
                {
                    dict = new Dictionary<string, ConfusableMapping>(StringComparer.Ordinal);
                    multi[mapping.SourceLength] = dict;
                }
                dict[mapping.Source] = mapping;
                if (mapping.SourceLength > maxSequenceLength)
                {
                    maxSequenceLength = mapping.SourceLength;
                }
            }

            // Canonicalization remains conservative: only single-character source and target pairs.
            if (mapping.SourceLength == 1 && mapping.TargetLength == 1)
            {
                canonical[mapping.Source] = mapping.Target;
            }
        }

        foreach (var ascii in AsciiHomoglyphLoader.Entries)
        {
            if (string.IsNullOrEmpty(ascii.Source) || string.IsNullOrEmpty(ascii.Target))
            {
                continue;
            }

            var asciiMapping = new ConfusableMapping(ascii.Source, ascii.Target, "ASCII", ascii.Category);
            asciiCanonical[asciiMapping.Source] = asciiMapping.Target;

            if (asciiMapping.SourceLength == 1)
            {
                map.TryAdd(asciiMapping.Source, asciiMapping);
            }
            else
            {
                if (!multi.TryGetValue(asciiMapping.SourceLength, out var dict))
                {
                    dict = new Dictionary<string, ConfusableMapping>(StringComparer.OrdinalIgnoreCase);
                    multi[asciiMapping.SourceLength] = dict;
                }
                dict[asciiMapping.Source] = asciiMapping;
                if (asciiMapping.SourceLength > maxSequenceLength)
                {
                    maxSequenceLength = asciiMapping.SourceLength;
                }
            }
        }

        return new ConfusablesIndex(map, multi, canonical, asciiCanonical, maxSequenceLength);
    }
}

internal readonly record struct ConfusableMapping(
    string Source,
    string Target,
    string? Type,
    string? Comment)
{
    public int SourceLength => Source.Length;

    public int TargetLength => Target.Length;

    public bool IsAsciiPair =>
        Source.All(static c => c <= 0x7F) &&
        Target.All(static c => c <= 0x7F);

    public bool IsIdentical =>
        string.Equals(Source, Target, StringComparison.Ordinal);
}

internal static class ReadOnlyDictionaryEmpty<TKey, TValue>
{
    public static IReadOnlyDictionary<TKey, TValue> Value { get; } =
        new Dictionary<TKey, TValue>();
}
