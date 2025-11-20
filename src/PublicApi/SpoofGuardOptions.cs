namespace UnicodeSpoofGuard;

/// <summary>
/// Configures runtime behaviour of the Unicode spoof detection pipeline.
/// </summary>
public sealed class SpoofGuardOptions
{
    /// <summary>
    /// Shared immutable instance representing the library defaults.
    /// </summary>
    public static SpoofGuardOptions Default { get; } = new() { EnableThreatIntel = true };

    /// <summary>
    /// When enabled, relaxes the default heuristics to reveal additional findings,
    /// including ASCII-only confusables and identical mappings.
    /// </summary>
    public bool StrictMode { get; init; }

    /// <summary>
    /// Optional locale hint (IETF language tag) that selects locale-aware policy profiles.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Explicit policy profile key. Overrides any locale-based resolution when set.
    /// </summary>
    public string? PolicyProfile { get; init; }

    /// <summary>
    /// Enables threat-intelligence matching against bundled or synced indicator feeds.
    /// </summary>
    public bool EnableThreatIntel { get; init; } = true;

    /// <summary>
    /// Creates a copy of the current options with <see cref="StrictMode"/> set to the provided value.
    /// </summary>
    public SpoofGuardOptions WithStrictMode(bool strictMode) =>
        new()
        {
            StrictMode = strictMode,
            Locale = Locale,
            PolicyProfile = PolicyProfile,
            EnableThreatIntel = EnableThreatIntel
        };

    /// <summary>
    /// Creates a copy of the current options with <see cref="Locale"/> set to the provided value.
    /// </summary>
    public SpoofGuardOptions WithLocale(string? locale) =>
        new()
        {
            StrictMode = StrictMode,
            Locale = locale,
            PolicyProfile = PolicyProfile,
            EnableThreatIntel = EnableThreatIntel
        };

    /// <summary>
    /// Creates a copy of the current options with <see cref="PolicyProfile"/> set to the provided value.
    /// </summary>
    public SpoofGuardOptions WithPolicyProfile(string? policyProfile) =>
        new()
        {
            StrictMode = StrictMode,
            Locale = Locale,
            PolicyProfile = policyProfile,
            EnableThreatIntel = EnableThreatIntel
        };

    /// <summary>
    /// Creates a copy of the current options with <see cref="EnableThreatIntel"/> set to the provided value.
    /// </summary>
    public SpoofGuardOptions WithThreatIntel(bool enabled) =>
        new()
        {
            StrictMode = StrictMode,
            Locale = Locale,
            PolicyProfile = PolicyProfile,
            EnableThreatIntel = enabled
        };
}

