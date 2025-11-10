using OutSystems.ExternalLibraries.SDK;
using System.Collections.Generic;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard
{
    [OSInterface(Description = "Security toolkit that detects Unicode spoofing attempts in user-provided identifiers.", Name = "UnicodeSpoofGuard", IconResourceName = "UnicodeSpoofGuard.resources.UnicodeSpoofGuard.png")]
    public interface IUnicodeSpoofGuard
    {
        [OSAction(Description = "Evaluates an email address for Unicode spoofing patterns and returns a full analysis report.", ReturnName = "Result")]
        AnalysisResult IsSafeEmail(
            [OSParameter(Description = "Email address provided by the end user.")]
            string email);

        [OSAction(Description = "Evaluates a domain or host name for Unicode spoofing indicators.", ReturnName = "Result")]
        AnalysisResult IsSafeDomain(
            [OSParameter(Description = "Domain or host string to validate before usage or storage.")]
            string domain);

        [OSAction(Description = "Checks a username or identifier for mixed-script, homoglyph, or invisible spoofing tactics.", ReturnName = "Result")]
        AnalysisResult IsSafeUsername(
            [OSParameter(Description = "User-provided identifier such as username, handle, or account id.")]
            string username);

        [OSAction(Description = "Generates a canonical representation of the supplied text using trusted single-character replacements.", ReturnName = "CanonicalText")]
        string GetCanonicalString(
            [OSParameter(Description = "Text to normalize before comparisons or storage.")]
            string text);

        [OSAction(Description = "Performs deep Unicode spoofing analysis and returns every finding detected in the text.", ReturnName = "Findings")]
        List<DetailedFinding> AnalyzeTextForSpoofing(
            [OSParameter(Description = "Text input that should be inspected for spoofing patterns.")]
            string text);
    }
}
