namespace UnicodeSpoofGuard;

/// <summary>
/// Per-request metadata that can adjust spoof detection behaviour.
/// </summary>
public sealed class AnalysisContext
{
    /// <summary>
    /// Optional locale hint (IETF language tag) for this analysis.
    /// When provided, it overrides the locale configured in <see cref="SpoofGuardOptions"/>.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Explicit policy profile identifier to evaluate during this analysis.
    /// Overrides both the locale-derived profile and the default profile.
    /// </summary>
    public string? PolicyProfile { get; init; }

    /// <summary>
    /// Optional strict-mode override for this analysis.
    /// </summary>
    public bool? StrictMode { get; init; }

    /// <summary>
    /// Optional override that disables or enables threat-intel matching.
    /// </summary>
    public bool? EnableThreatIntel { get; init; }
}

