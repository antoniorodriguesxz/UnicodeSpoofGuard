using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnicodeSpoofGuard.Data;

internal static class PolicyConfigLoader
{
    private const string ResourceName = "UnicodeSpoofGuard.config.policies.json";
    private static readonly Lazy<PolicyConfig> Cached = new(Load, isThreadSafe: true);

    public static PolicyConfig Config => Cached.Value;

    private static PolicyConfig Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                         ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' was not found.");

        var config = JsonSerializer.Deserialize<PolicyConfig>(stream, JsonOptions)
                     ?? throw new InvalidOperationException("Unable to deserialize policy configuration.");

        if (string.IsNullOrWhiteSpace(config.DefaultProfile))
        {
            throw new InvalidOperationException("Policy configuration must specify a defaultProfile.");
        }

        if (!config.Profiles.TryGetValue(config.DefaultProfile, out _))
        {
            throw new InvalidOperationException($"Default policy profile '{config.DefaultProfile}' is not defined.");
        }

        foreach (var kvp in config.Profiles)
        {
            kvp.Value.Initialize();
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

internal sealed class PolicyConfig
{
    [JsonPropertyName("defaultProfile")]
    public string DefaultProfile { get; set; } = "global";

    [JsonPropertyName("profiles")]
    public Dictionary<string, PolicyProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PolicyProfile Resolve(string? profileName, string? locale)
    {
        if (!string.IsNullOrWhiteSpace(profileName) && Profiles.TryGetValue(profileName, out var profileByName))
        {
            return profileByName;
        }

        if (!string.IsNullOrWhiteSpace(locale))
        {
            foreach (var profile in Profiles.Values)
            {
                if (profile.MatchesLocale(locale))
                {
                    return profile;
                }
            }
        }

        if (Profiles.TryGetValue(DefaultProfile, out var fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException($"Default policy profile '{DefaultProfile}' is not defined.");
    }
}

internal sealed class PolicyProfile
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("locales")]
    public List<string> Locales { get; set; } = new();

    [JsonPropertyName("allowedMixedScripts")]
    public List<List<string>> AllowedMixedScripts { get; set; } = new();

    private List<HashSet<string>> _normalizedAllowedScripts = new();

    internal void Initialize()
    {
        _normalizedAllowedScripts = AllowedMixedScripts
            .Where(list => list is { Count: > 0 })
            .Select(list => list
                .Select(NormalizeScript)
                .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    internal bool MatchesLocale(string locale)
    {
        if (Locales.Count == 0)
        {
            return false;
        }

        var normalized = NormalizeLocale(locale);
        foreach (var candidate in Locales)
        {
            var normalizedCandidate = NormalizeLocale(candidate);
            if (normalized.Equals(normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.StartsWith(normalizedCandidate + "-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsMixedScriptAllowed(IReadOnlyCollection<string> scripts)
    {
        if (scripts.Count <= 1)
        {
            return true;
        }

        if (_normalizedAllowedScripts.Count == 0)
        {
            return false;
        }

        var normalizedScripts = scripts.Select(NormalizeScript)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _normalizedAllowedScripts.Any(allowed =>
            allowed.SetEquals(normalizedScripts));
    }

    private static string NormalizeLocale(string value) =>
        value.Replace('_', '-').ToLowerInvariant();

    private static string NormalizeScript(string value) =>
        value.Trim();
}

