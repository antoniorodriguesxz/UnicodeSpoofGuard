using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnicodeSpoofGuard.Data;
using UnicodeSpoofGuard.Feeds;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard.Detection
{
    public class SpoofDetector
    {
        private static readonly HashSet<char> BidirectionalControlChars = new()
        {
            '\u202A', '\u202B', '\u202C', '\u202D', '\u202E', '\u2066', '\u2067', '\u2068', '\u2069'
        };

        private readonly DetectionConfig _config;
        private readonly SpoofGuardOptions _options;
        private readonly PolicyConfig _policyConfig;
        private readonly ThreatIndicatorStore _threatStore;
        private readonly ConfusablesIndex? _customIndex;

        public SpoofDetector()
            : this(null, DetectionConfigLoader.Config, PolicyConfigLoader.Config, SpoofGuardOptions.Default, ThreatIndicatorStore.Instance)
        {
        }

        public SpoofDetector(SpoofGuardOptions options)
            : this(null, DetectionConfigLoader.Config, PolicyConfigLoader.Config, options, ThreatIndicatorStore.Instance)
        {
        }

        internal SpoofDetector(
            DetectionConfig config,
            PolicyConfig policyConfig,
            SpoofGuardOptions options,
            ThreatIndicatorStore? threatStore = null)
            : this(null, config, policyConfig, options, threatStore)
        {
        }

        internal SpoofDetector(
            ConfusablesIndex? confusablesIndex,
            DetectionConfig config,
            PolicyConfig policyConfig,
            SpoofGuardOptions options,
            ThreatIndicatorStore? threatStore = null)
        {
            _customIndex = confusablesIndex;
            _config = config;
            _policyConfig = policyConfig;
            _options = options;
            _threatStore = threatStore ?? ThreatIndicatorStore.Instance;
        }

        public List<DetailedFinding> Analyze(string text)
        {
            var findings = new List<DetailedFinding>();
            var confusablesIndex = _customIndex ?? ConfusablesIndex.Instance;
            string? canonicalText = null;

            var profile = _config.GetProfile(_options.StrictMode);
            var policyProfile = _policyConfig.Resolve(_options.PolicyProfile, _options.Locale);

            DetectHomoglyphs(text, confusablesIndex, profile, findings);
            DetectMixedScripts(text, policyProfile, findings);
            DetectInvisibleCharacters(text, findings);
            DetectBidirectionalControlCharacters(text, findings);

            if (_options.EnableThreatIntel)
            {
                canonicalText ??= Canonicalizer.ToCanonical(text);
                DetectThreatIndicators(text, canonicalText, findings);
            }

            return findings.OrderBy(f => f.Position).ToList();
        }

        private void DetectHomoglyphs(string text, ConfusablesIndex confusablesIndex, DetectionProfile profile, List<DetailedFinding> findings)
        {
            int i = 0;
            int maxSequence = Math.Max(1, confusablesIndex.MaxSequenceLength);

            while (i < text.Length)
            {
                var remaining = text.Length - i;
                var limit = Math.Min(maxSequence, remaining);
                ConfusableMapping mapping = default;
                int matchedLength = 0;

                for (int length = limit; length >= 1; length--)
                {
                    var segment = text.Substring(i, length);
                    if (length > 1)
                    {
                        var map = confusablesIndex.GetMultiCharMappings(length);
                        if (map.Count == 0 || !map.TryGetValue(segment, out mapping))
                        {
                            continue;
                        }
                    }
                    else if (!confusablesIndex.TryGetSingleCharMapping(segment, out mapping))
                    {
                        continue;
                    }

                    matchedLength = length;
                    break;
                }

                if (matchedLength > 0 && ShouldFlag(mapping, profile, out _))
                {
                    var substring = text.Substring(i, matchedLength);
                    findings.Add(new DetailedFinding
                    {
                        FindingType = "Homoglyph",
                        Description = $"Substring '{substring}' at position {i} is a homoglyph of '{mapping.Target}'.",
                        Position = i,
                        Substring = substring
                    });
                }

                i += matchedLength > 0 ? matchedLength : 1;
            }
        }

        private void DetectMixedScripts(string text, PolicyProfile policyProfile, List<DetailedFinding> findings)
        {
            var scripts = new HashSet<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetter(text[i]))
                {
                    if ((text[i] >= 0x0041 && text[i] <= 0x005A) || (text[i] >= 0x0061 && text[i] <= 0x007A))
                    {
                        scripts.Add("Latin");
                    }
                    else if (text[i] >= 0x0400 && text[i] <= 0x04FF)
                    {
                        scripts.Add("Cyrillic");
                    }
                    else if (text[i] >= 0x0370 && text[i] <= 0x03FF)
                    {
                        scripts.Add("Greek");
                    }
                }
            }

            if (scripts.Count > 1 && !policyProfile.IsMixedScriptAllowed(scripts))
            {
                findings.Add(new DetailedFinding
                {
                    FindingType = "MixedScript",
                    Description = $"The text contains characters from multiple scripts: {string.Join(", ", scripts)}.",
                    Position = 0,
                    Substring = text
                });
            }
        }
        
        private void DetectInvisibleCharacters(string text, List<DetailedFinding> findings)
        {
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var category = char.GetUnicodeCategory(c);
                if (category == UnicodeCategory.Control || 
                    category == UnicodeCategory.Format || 
                    (c != ' ' && category == UnicodeCategory.SpaceSeparator))
                {
                    findings.Add(new DetailedFinding
                    {
                        FindingType = "InvisibleCharacter",
                        Description = $"Invisible character of category '{category}' found at position {i}.",
                        Position = i,
                        Substring = c.ToString()
                    });
                }
            }
        }

        private void DetectBidirectionalControlCharacters(string text, List<DetailedFinding> findings)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (BidirectionalControlChars.Contains(text[i]))
                {
                    findings.Add(new DetailedFinding
                    {
                        FindingType = "BidirectionalControl",
                        Description = $"Bidirectional control character found at position {i}.",
                        Position = i,
                        Substring = text[i].ToString()
                    });
                }
            }
        }

        private void DetectThreatIndicators(string rawText, string canonicalText, List<DetailedFinding> findings)
        {
            if (!_options.EnableThreatIntel)
            {
                return;
            }

            foreach (var (token, indicator) in _threatStore.FindMatches(canonicalText))
            {
                var position = FindApproximatePosition(rawText, token);
                var substring = position >= 0 && position < rawText.Length
                    ? rawText.Substring(position, Math.Min(token.Length, rawText.Length - position))
                    : token;

                findings.Add(new DetailedFinding
                {
                    FindingType = "ThreatIntel",
                    Description = $"Canonical token '{token}' matches threat feed '{indicator.Source}'.",
                    Position = Math.Max(position, 0),
                    Substring = substring
                });
            }
        }

        private static int FindApproximatePosition(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
            {
                return 0;
            }

            var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return index;
            }

            var normalizedToken = token
                .Replace('0', 'o')
                .Replace('1', 'l');

            index = text.IndexOf(normalizedToken, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? index : 0;
        }

        public static string GetCanonicalString(string text)
        {
            return Canonicalizer.ToCanonical(text);
        }

        private static bool ShouldFlag(ConfusableMapping mapping, DetectionProfile profile, out double score)
        {
            score = 1.0;

            if (mapping.IsIdentical)
            {
                if (profile.SuppressIdenticalMappings)
                {
                    score = 0;
                    return false;
                }
                score = Math.Min(score, 0.2);
            }

            if (mapping.IsAsciiPair)
            {
                if (profile.SuppressAsciiConfusables)
                {
                    score = 0;
                    return false;
                }
                score = Math.Min(score, 0.4);
            }

            var threshold = profile.GetThreshold(mapping.Type);
            return score >= threshold;
        }
    }
}
