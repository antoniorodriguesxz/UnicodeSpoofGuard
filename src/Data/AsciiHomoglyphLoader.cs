using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnicodeSpoofGuard.Data;

internal static class AsciiHomoglyphLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.data.ascii_homoglyphs.json";
    private static readonly Lazy<IReadOnlyList<AsciiHomoglyphEntry>> Cached = new(Load, isThreadSafe: true);

    public static IReadOnlyList<AsciiHomoglyphEntry> Entries => Cached.Value;

    private static IReadOnlyList<AsciiHomoglyphEntry> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");

        var container = JsonSerializer.Deserialize<AsciiHomoglyphContainer>(stream, JsonOptions)
                        ?? throw new InvalidOperationException("Unable to deserialize ASCII homoglyph dataset.");

        return container.Mappings;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed class AsciiHomoglyphContainer
{
    [JsonPropertyName("mappings")]
    public List<AsciiHomoglyphEntry> Mappings { get; set; } = new();
}

internal sealed record AsciiHomoglyphEntry(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("category")] string? Category);

