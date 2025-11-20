using OutSystems.ExternalLibraries.SDK;
using System.Collections.Generic;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard
{
    [OSInterface(Description = "Security toolkit that detects Unicode spoofing attempts in user-provided identifiers.", Name = "UnicodeSpoofGuard", IconResourceName = "UnicodeSpoofGuard.resources.UnicodeSpoofGuard.png")]
    public interface IUnicodeSpoofGuard
    {
        [OSAction(Description = "Evaluates an email address for Unicode spoofing patterns and returns a full analysis report.", ReturnName = "Result")]
        AnalysisResult IsSafeEmail(
            [OSParameter(Description = "Email address provided by the end user.")]
            string Email,
            [OSParameter(Description = "Optional per-call overrides controlling strict mode, locale, and feed usage.")]
            AnalysisOptions? Options = null);

        [OSAction(Description = "Evaluates a domain or host name for Unicode spoofing indicators.", ReturnName = "Result")]
        AnalysisResult IsSafeDomain(
            [OSParameter(Description = "Domain or host string to validate before usage or storage.")]
            string Domain,
            [OSParameter(Description = "Optional per-call overrides controlling strict mode, locale, and feed usage.")]
            AnalysisOptions? Options = null);

        [OSAction(Description = "Checks a username or identifier for mixed-script, homoglyph, or invisible spoofing tactics.", ReturnName = "Result")]
        AnalysisResult IsSafeUsername(
            [OSParameter(Description = "User-provided identifier such as username, handle, or account id.")]
            string Username,
            [OSParameter(Description = "Optional per-call overrides controlling strict mode, locale, and feed usage.")]
            AnalysisOptions? Options = null);

        [OSAction(Description = "Generates a canonical representation of the supplied text using trusted single-character replacements.", ReturnName = "CanonicalText")]
        string GetCanonicalString(
            [OSParameter(Description = "Text to normalize before comparisons or storage.")]
            string Text);

        [OSAction(Description = "Performs deep Unicode spoofing analysis and returns every finding detected in the text.", ReturnName = "Findings")]
        List<DetailedFinding> AnalyzeTextForSpoofing(
            [OSParameter(Description = "Text input that should be inspected for spoofing patterns.")]
            string Text,
            [OSParameter(Description = "Optional per-call overrides controlling strict mode, locale, and feed usage.")]
            AnalysisOptions? Options = null);

        [OSAction(Description = "Downloads or ingests a Unicode confusables dataset and refreshes the in-memory mappings.", ReturnName = "Result")]
        RefreshConfusablesResult RefreshConfusablesData(
            [OSParameter(Description = "HTTPS URL pointing to a valid Unicode confusables.txt file.")]
            string ConfusablesUrl = ConfusablesUpdateService.DefaultConfusablesUrl,
            [OSParameter(Description = "Optional binary contents of confusables.txt. When supplied, the download step is skipped.")]
            byte[]? ConfusablesContent = null);
    }
}
