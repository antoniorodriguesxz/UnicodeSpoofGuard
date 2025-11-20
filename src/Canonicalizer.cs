using System.Text;
using UnicodeSpoofGuard.Detection;

namespace UnicodeSpoofGuard;

internal static class Canonicalizer
{
    public static string ToCanonical(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var normalized = text.Normalize(NormalizationForm.FormKC);
        return ApplyMappings(normalized, ConfusablesIndex.Instance);
    }

    private static string ApplyMappings(string text, ConfusablesIndex index)
    {
        var builder = new StringBuilder(text.Length);
        var singleMap = index.GetSingleCharCanonicalMap();
        var asciiMap = index.GetAsciiCanonicalMap();
        int maxSequence = Math.Max(1, index.MaxSequenceLength);

        int i = 0;
        while (i < text.Length)
        {
            var remaining = text.Length - i;
            var limit = Math.Min(maxSequence, remaining);
            var matched = false;

            for (int length = limit; length >= 1; length--)
            {
                var segment = text.Substring(i, length);

                if (length > 1)
                {
                    var map = index.GetMultiCharMappings(length);
                    if (map.Count > 0 && map.TryGetValue(segment, out var mapping))
                    {
                        builder.Append(mapping.Target);
                        i += length;
                        matched = true;
                        break;
                    }

                    if (asciiMap.Count > 0 && asciiMap.TryGetValue(segment, out var asciiTarget))
                    {
                        builder.Append(asciiTarget);
                        i += length;
                        matched = true;
                        break;
                    }
                }
                else
                {
                    if (singleMap.TryGetValue(segment, out var canonical))
                    {
                        builder.Append(canonical);
                        i += 1;
                        matched = true;
                        break;
                    }

                    if (asciiMap.TryGetValue(segment, out var asciiTarget))
                    {
                        builder.Append(asciiTarget);
                        i += 1;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched)
            {
                builder.Append(text[i]);
                i++;
            }
        }

        return builder.ToString();
    }
}

