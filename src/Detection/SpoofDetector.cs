using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnicodeSpoofGuard.Structures;

namespace UnicodeSpoofGuard.Detection
{
    public class SpoofDetector
    {
        private static readonly Lazy<Dictionary<string, string>> AllConfusablesMap =
            new Lazy<Dictionary<string, string>>(ConfusablesParser.GetAllConfusables);
        
        private static readonly Lazy<Dictionary<string, string>> SingleCharCanonicalMap =
            new Lazy<Dictionary<string, string>>(ConfusablesParser.GetSingleCharCanonicalMap);
        
        private static readonly HashSet<char> BidirectionalControlChars = new HashSet<char>
        {
            '\u202A', '\u202B', '\u202C', '\u202D', '\u202E', '\u2066', '\u2067', '\u2068', '\u2069'
        };

        public List<DetailedFinding> Analyze(string text)
        {
            var findings = new List<DetailedFinding>();
            
            DetectHomoglyphs(text, findings);
            DetectMixedScripts(text, findings);
            DetectInvisibleCharacters(text, findings);
            DetectBidirectionalControlCharacters(text, findings);

            return findings.OrderBy(f => f.Position).ToList();
        }

        private void DetectHomoglyphs(string text, List<DetailedFinding> findings)
        {
            for (int i = 0; i < text.Length; i++)
            {
                var c = text.Substring(i, 1);
                if (AllConfusablesMap.Value.TryGetValue(c, out var canonicalChar))
                {
                    findings.Add(new DetailedFinding
                    {
                        FindingType = "Homoglyph",
                        Description = $"Character '{c}' at position {i} is a homoglyph of '{canonicalChar}'.",
                        Position = i,
                        Substring = c
                    });
                }
            }
        }

        private void DetectMixedScripts(string text, List<DetailedFinding> findings)
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

            if (scripts.Count > 1)
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

        public static string GetCanonicalString(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                var c = text.Substring(i, 1);
                if (SingleCharCanonicalMap.Value.TryGetValue(c, out var canonical))
                {
                    result.Append(canonical);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}
