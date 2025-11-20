using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnicodeSpoofGuard.Data;

internal static class DetectionConfigLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.config.detection.json";
    private static readonly Lazy<DetectionConfig> Cached = new(Load, isThreadSafe: true);

    public static DetectionConfig Config => Cached.Value;

    private static DetectionConfig Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");
        var config = JsonSerializer.Deserialize<DetectionConfig>(stream, JsonOptions)
                     ?? throw new InvalidOperationException("Unable to deserialize detection configuration.");

        if (string.IsNullOrWhiteSpace(config.DefaultMode))
        {
            throw new InvalidOperationException("Detection configuration must specify a defaultMode.");
        }

        if (!config.Modes.TryGetValue(config.DefaultMode, out _))
        {
            throw new InvalidOperationException($"Default mode '{config.DefaultMode}' is not defined in modes.");
        }

        return config;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed class DetectionConfig
{
    [JsonPropertyName("defaultMode")]
    public string DefaultMode { get; set; } = "balanced";

    [JsonPropertyName("modes")]
    public Dictionary<string, DetectionProfile> Modes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DetectionProfile GetProfile(bool strictMode)
    {
        var key = strictMode ? "strict" : DefaultMode;
        if (!Modes.TryGetValue(key, out var profile))
        {
            profile = Modes.Values.FirstOrDefault()
                      ?? throw new InvalidOperationException("Detection configuration does not contain any modes.");
        }
        return profile;
    }
}

internal sealed class DetectionProfile
{
    [JsonPropertyName("suppressAsciiConfusables")]
    public bool SuppressAsciiConfusables { get; set; }

    [JsonPropertyName("suppressIdenticalMappings")]
    public bool SuppressIdenticalMappings { get; set; }

    [JsonPropertyName("confusableClassThresholds")]
    public Dictionary<string, double> ConfusableClassThresholds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public double GetThreshold(string? confusableClass)
    {
        if (confusableClass is not null && ConfusableClassThresholds.TryGetValue(confusableClass, out var value))
        {
            return value;
        }

        if (ConfusableClassThresholds.TryGetValue("default", out var fallback))
        {
            return fallback;
        }

        return 0.5;
    }
}

