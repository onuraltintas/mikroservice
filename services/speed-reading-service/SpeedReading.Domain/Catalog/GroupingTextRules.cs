using System.Text.RegularExpressions;

namespace SpeedReading.Domain.Catalog;

public sealed record GroupingBoundary(int EndIndex, bool HasNewline);

/// <summary>Fixed-size word groups split at sentence/paragraph ends, not semantic parsing.</summary>
public static class GroupingTextRules
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
        { "dr.", "prof.", "doç.", "av.", "sn.", "vb.", "vd.", "vs.", "örn.", "bkz." };

    public static List<GroupingBoundary> Build(string text, int chunkSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (chunkSize is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        var tokens = Regex.Matches(text, @"\S+");
        var groups = new List<GroupingBoundary>();
        var count = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            count++;
            var token = tokens[index];
            var nextOffset = index + 1 < tokens.Count ? tokens[index + 1].Index : text.Length;
            var paragraphEnd = text.AsSpan(token.Index + token.Length, nextOffset - token.Index - token.Length).Contains('\n');
            var word = token.Value.TrimEnd('"', '\'', '”', '’', ')', ']', '}');
            var sentenceEnd = word.EndsWith('!') || word.EndsWith('?') || word.EndsWith('…')
                || (word.EndsWith('.') && !Abbreviations.Contains(word)
                    && !Regex.IsMatch(word, @"^\d+\.$") && !Regex.IsMatch(word, @"^[A-Za-zÇĞİÖŞÜçğıöşü]\.$"));
            if (count == chunkSize || sentenceEnd || paragraphEnd || index == tokens.Count - 1)
            {
                groups.Add(new(index + 1, paragraphEnd));
                count = 0;
            }
        }
        return groups;
    }
}
