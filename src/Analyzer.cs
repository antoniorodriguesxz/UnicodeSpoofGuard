using UnicodeSpoofGuard.Detection;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard;

/// <summary>
/// High-level entry point that orchestrates spoof detection using configurable policies and heuristics.
/// </summary>
public sealed class Analyzer
{
    private readonly SpoofGuardOptions _options;
    private readonly SpoofDetector _detector;

    public Analyzer()
        : this(SpoofGuardOptions.Default)
    {
    }

    public Analyzer(SpoofGuardOptions? options)
    {
        _options = options ?? SpoofGuardOptions.Default;
        _detector = new SpoofDetector(_options);
    }

    /// <summary>
    /// Executes the spoof analysis and returns a summarized result.
    /// </summary>
    public AnalysisResult Analyze(string text, AnalysisContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AnalysisResult
            {
                IsSafe = true,
                Reason = string.Empty,
                CanonicalValue = text ?? string.Empty,
                Findings = new List<DetailedFinding>()
            };
        }

        var detector = GetDetector(context);
        var findings = detector.Analyze(text);
        bool isSafe = findings.Count == 0;

        return new AnalysisResult
        {
            IsSafe = isSafe,
            Reason = isSafe ? string.Empty : findings[0].Description,
            CanonicalValue = SpoofDetector.GetCanonicalString(text),
            Findings = findings
        };
    }

    /// <summary>
    /// Runs spoof analysis and returns every finding without additional aggregation.
    /// </summary>
    public List<DetailedFinding> AnalyzeDetailed(string text, AnalysisContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<DetailedFinding>();
        }

        var detector = GetDetector(context);
        return detector.Analyze(text);
    }

    private SpoofDetector GetDetector(AnalysisContext? context)
    {
        if (context is null)
        {
            return _detector;
        }

        var effectiveOptions = MergeOptions(context);
        return OptionsEqual(effectiveOptions, _options)
            ? _detector
            : new SpoofDetector(effectiveOptions);
    }

    private SpoofGuardOptions MergeOptions(AnalysisContext context) =>
        new()
        {
            StrictMode = context.StrictMode ?? _options.StrictMode,
            Locale = context.Locale ?? _options.Locale,
            PolicyProfile = context.PolicyProfile ?? _options.PolicyProfile,
            EnableThreatIntel = context.EnableThreatIntel ?? _options.EnableThreatIntel
        };

    private static bool OptionsEqual(SpoofGuardOptions left, SpoofGuardOptions right) =>
        left.StrictMode == right.StrictMode &&
        string.Equals(left.Locale, right.Locale, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.PolicyProfile, right.PolicyProfile, StringComparison.OrdinalIgnoreCase) &&
        left.EnableThreatIntel == right.EnableThreatIntel;
}

