using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace UnicodeSpoofGuard.Detection
{
    public class ConfusablesParser
    {
        private static readonly Dictionary<string, string> AllConfusablesMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> SingleCharCanonicalMap = new Dictionary<string, string>();
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        private static string CodePointsToString(string hexes)
        {
            var sb = new StringBuilder();
            foreach (var hex in hexes.Split(' '))
            {
                if (string.IsNullOrWhiteSpace(hex)) continue;
                int codePoint = int.Parse(hex, NumberStyles.HexNumber);
                sb.Append(char.ConvertFromUtf32(codePoint));
            }
            return sb.ToString();
        }

        private static bool IsBasicLatin(string value)
        {
            foreach (var rune in value.EnumerateRunes())
            {
                if (rune.Value > 0x007F)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "UnicodeSpoofGuard.confusables.txt";

                using (var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new FileNotFoundException("Could not find the embedded confusables.txt file.", resourceName))
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                            var parts = line.Split(';');
                            if (parts.Length < 2) continue;
                            
                            var confusableHex = parts[0].Trim();
                            var canonicalHex = parts[1].Trim();

                            var confusableString = CodePointsToString(confusableHex);
                            var canonicalString = CodePointsToString(canonicalHex);
                            
                            if(string.IsNullOrEmpty(confusableString) || string.IsNullOrEmpty(canonicalString)) continue;
                            
                            // A character is only a homoglyph threat if it's different from its canonical form.
                            if(confusableString.Equals(canonicalString, StringComparison.Ordinal)) continue;

                            // Skip confusables that are entirely within the basic Latin range; these produce too many false positives.
                            if (IsBasicLatin(confusableString)) continue;

                            // Populate the map for all confusables used in detection
                            AllConfusablesMap[confusableString] = canonicalString;
                            
                            // Populate the conservative map for canonicalization, only keeping single-character mappings
                            if (confusableString.Length == 1 && canonicalString.Length == 1)
                            {
                                // Still allow canonical mapping for ASCII confusables as long as replacement is safe and differs.
                                SingleCharCanonicalMap[confusableString] = canonicalString;
                            }
                        }
                    }
                }
                _isInitialized = true;
            }
        }

        public static Dictionary<string, string> GetAllConfusables()
        {
            if (!_isInitialized) Initialize();
            return AllConfusablesMap;
        }
        
        public static Dictionary<string, string> GetSingleCharCanonicalMap()
        {
            if (!_isInitialized) Initialize();
            return SingleCharCanonicalMap;
        }
    }
}
