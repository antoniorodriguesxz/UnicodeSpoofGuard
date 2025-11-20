using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace UnicodeSpoofGuard.Data;

internal static class ConfusablesTextParser
{
    public static List<ConfusableEntry> Parse(string content)
    {
        var result = new List<ConfusableEntry>();
        using var reader = new StringReader(content);
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var commentSplit = line.Split('#', 2);
            var payload = commentSplit[0].Trim();
            var comment = commentSplit.Length > 1 ? commentSplit[1].Trim() : null;

            var parts = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var source = HexToString(parts[0]);
            var target = HexToString(parts[1]);
            var type = parts.Length > 2 ? parts[2].Trim() : null;

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                continue;
            }

            result.Add(new ConfusableEntry(source, target, type, comment));
        }

        return result;
    }

    private static string HexToString(string hexSequence)
    {
        var builder = new StringBuilder();
        foreach (var token in hexSequence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                builder.Append(char.ConvertFromUtf32(codePoint));
            }
        }
        return builder.ToString();
    }
}

