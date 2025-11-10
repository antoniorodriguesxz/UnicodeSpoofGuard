using System.Collections.Generic;
using System.Linq;
using UnicodeSpoofGuard.Detection;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard
{
    public class UnicodeSpoofGuard : IUnicodeSpoofGuard
    {
        private readonly SpoofDetector _spoofDetector = new SpoofDetector();

        public AnalysisResult IsSafeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }

            var findings = _spoofDetector.Analyze(email);
            bool isSafe = !findings.Any();
            
            return new AnalysisResult
            {
                IsSafe = isSafe,
                Reason = isSafe ? string.Empty : findings.First().Description,
                CanonicalValue = GetCanonicalString(email),
                Findings = findings
            };
        }

        public AnalysisResult IsSafeDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }
            
            var findings = _spoofDetector.Analyze(domain);
            bool isSafe = !findings.Any();

            return new AnalysisResult
            {
                IsSafe = isSafe,
                Reason = isSafe ? string.Empty : findings.First().Description,
                CanonicalValue = GetCanonicalString(domain),
                Findings = findings
            };
        }

        public AnalysisResult IsSafeUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new AnalysisResult { IsSafe = true, Findings = new List<DetailedFinding>() };
            }

            var findings = _spoofDetector.Analyze(username);
            bool isSafe = !findings.Any();

            return new AnalysisResult
            {
                IsSafe = isSafe,
                Reason = isSafe ? string.Empty : findings.First().Description,
                CanonicalValue = GetCanonicalString(username),
                Findings = findings
            };
        }

        public string GetCanonicalString(string text)
        {
            return SpoofDetector.GetCanonicalString(text);
        }

        public List<DetailedFinding> AnalyzeTextForSpoofing(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<DetailedFinding>();
            }
            return _spoofDetector.Analyze(text);
        }
    }
}
