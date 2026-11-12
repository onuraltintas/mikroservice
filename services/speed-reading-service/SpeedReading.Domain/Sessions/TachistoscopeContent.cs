namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Two-word Tachistoscope phrases. Words are paired at random, fixed per session by a seed, instead of
/// with their alphabetical neighbour; every length in the adaptive range gets its own share of pairs so a
/// stimulus near the target length is always available when the vocabulary allows it.
/// </summary>
public static class TachistoscopeContent
{
    public const int RandomPairs = 300;
    public const int PairsPerLength = 24;

    public static string[] BuildPhrasePool(IEnumerable<string> words, int seed, int minLength, int maxLength)
    {
        // Ordinal order makes the result independent of the database collation and query order.
        var distinct = words.Select(word => word.Trim()).Where(word => word.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (distinct.Length < 2) return [];
        var random = new Random(seed);
        var shuffled = distinct.ToArray();
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }

        var phrases = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool Add(string first, string second)
        {
            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) return false;
            var phrase = first + " " + second;
            if (!seen.Add(phrase)) return false;
            phrases.Add(phrase);
            return true;
        }

        for (var index = 0; index < Math.Min(RandomPairs, shuffled.Length); index++)
            Add(shuffled[index], shuffled[(index + 1) % shuffled.Length]);

        var byLength = shuffled.GroupBy(word => word.Length).ToDictionary(group => group.Key, group => group.ToArray());
        for (var length = Math.Max(3, minLength); length <= maxLength; length++)
        {
            var combinations = byLength.Keys.Where(first => byLength.ContainsKey(length - 1 - first))
                .Order().Select(first => (First: first, Second: length - 1 - first)).ToArray();
            if (combinations.Length == 0) continue;
            var count = phrases.Count(phrase => phrase.Length == length);
            for (var attempt = 0; count < PairsPerLength && attempt < PairsPerLength * 20; attempt++)
            {
                var (first, second) = combinations[random.Next(combinations.Length)];
                var firstWords = byLength[first];
                var secondWords = byLength[second];
                if (Add(firstWords[random.Next(firstWords.Length)], secondWords[random.Next(secondWords.Length)])) count++;
            }
        }
        return [.. phrases];
    }
}
