using System.Collections.Generic;
using System.Linq;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Detection;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard
{
    public class UnicodeSpoofGuard : IUnicodeSpoofGuard
    {
        private readonly Analyzer _analyzer;

        public UnicodeSpoofGuard()
            : this(SpoofGuardOptions.Default)
        {
        }

        public UnicodeSpoofGuard(SpoofGuardOptions? options)
        {
            var applied = options ?? SpoofGuardOptions.Default;
            _analyzer = new Analyzer(applied);
        }

        public AnalysisResult IsSafeEmail(string Email, AnalysisOptions? Options = null)
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }

            return _analyzer.Analyze(Email, CreateContext(Options));
        }

        public AnalysisResult IsSafeDomain(string Domain, AnalysisOptions? Options = null)
        {
            if (string.IsNullOrWhiteSpace(Domain))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }
            
            return _analyzer.Analyze(Domain, CreateContext(Options));
        }

        public AnalysisResult IsSafeUsername(string Username, AnalysisOptions? Options = null)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }

            return _analyzer.Analyze(Username, CreateContext(Options));
        }

        public string GetCanonicalString(string Text)
        {
            return SpoofDetector.GetCanonicalString(Text);
        }

        public List<DetailedFinding> AnalyzeTextForSpoofing(string Text, AnalysisOptions? Options = null)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return new List<DetailedFinding>();
            }
            return _analyzer.AnalyzeDetailed(Text, CreateContext(Options));
        }

        public AnalysisResult AnalyzeTextForSpoofing(string text, AnalysisContext context)
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

            return _analyzer.Analyze(text, context);
        }

        public List<DetailedFinding> AnalyzeTextForSpoofingDetailed(string text, AnalysisContext context)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<DetailedFinding>();
            }

            return _analyzer.AnalyzeDetailed(text, context);
        }

        public RefreshConfusablesResult RefreshConfusablesData(string ConfusablesUrl = ConfusablesUpdateService.DefaultConfusablesUrl, byte[]? ConfusablesContent = null)
        {
            return ConfusablesUpdateService.Refresh(ConfusablesUrl, ConfusablesContent);
        }

        private static AnalysisContext? CreateContext(AnalysisOptions? Options)
        {
            if (!Options.HasValue)
            {
                return null;
            }

            var value = Options.Value;
            bool? strictMode = value.UseStrictMode ? true : (bool?)null;
            bool? enableThreatIntel = value.DisableThreatIntel ? false : (bool?)null;
            var locale = string.IsNullOrWhiteSpace(value.Locale) ? null : value.Locale;
            var policy = string.IsNullOrWhiteSpace(value.PolicyProfile) ? null : value.PolicyProfile;

            if (strictMode is null && enableThreatIntel is null && locale is null && policy is null)
            {
                return null;
            }

            return new AnalysisContext
            {
                StrictMode = strictMode,
                EnableThreatIntel = enableThreatIntel,
                Locale = locale,
                PolicyProfile = policy
            };
        }
    }
}
