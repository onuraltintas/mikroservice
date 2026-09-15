using System.Text.RegularExpressions;

namespace SpeedReading.Application.Content;

public sealed record ReadingTextQualityDistribution(int Level, int Count);

public sealed record ReadingTextQualityMetrics(
    int WordCount,
    int SentenceCount,
    decimal AverageWordsPerSentence,
    decimal AverageCharactersPerWord,
    decimal? EstimatedAtesmanReadability,
    string? ReadabilityBand,
    IReadOnlyList<ReadingTextQualityDistribution> BloomDistribution,
    IReadOnlyList<ReadingTextQualityDistribution> DifficultyDistribution,
    IReadOnlyList<string> Warnings);

public static class TurkishReadingTextQualityAnalyzer
{
    private static readonly Regex WordPattern = new("\\p{L}+", RegexOptions.Compiled);
    private static readonly Regex SentencePattern = new("[.!?]+(?=\\s|$)", RegexOptions.Compiled);
    private const string TurkishVowels = "aeıioöuüAEIİOÖUÜ";

    public static ReadingTextQualityMetrics Analyze(
        string? content,
        string? language,
        IReadOnlyList<ReadingQuestionSummary> questions)
    {
        var words = WordPattern.Matches(content ?? string.Empty)
            .Select(match => match.Value)
            .ToArray();
        var wordCount = words.Length;
        var sentenceCount = Math.Max(
            SentencePattern.Matches(content ?? string.Empty).Count,
            wordCount == 0 ? 0 : 1);
        var averageWordsPerSentence = sentenceCount == 0
            ? 0
            : Math.Round((decimal)wordCount / sentenceCount, 1);
        var averageCharactersPerWord = wordCount == 0
            ? 0
            : Math.Round((decimal)words.Average(word => word.Length), 1);
        var isTurkish = string.Equals(language?.Trim(), "tr", StringComparison.OrdinalIgnoreCase);
        decimal? readability = isTurkish && wordCount > 0
            ? CalculateAtesmanReadability(words, averageWordsPerSentence)
            : null;
        var warnings = BuildWarnings(wordCount, averageWordsPerSentence, questions, readability).ToArray();

        return new ReadingTextQualityMetrics(
            wordCount,
            sentenceCount,
            averageWordsPerSentence,
            averageCharactersPerWord,
            readability,
            readability is null ? null : ReadabilityBand(readability.Value),
            Distribution(questions, question => question.BloomLevel),
            Distribution(questions, question => question.DifficultyLevel),
            warnings);
    }

    private static decimal CalculateAtesmanReadability(
        IReadOnlyList<string> words,
        decimal averageWordsPerSentence)
    {
        var syllablePerWord = words.Average(word => word.Count(character => TurkishVowels.Contains(character)));
        var score = 198.825m - 40.175m * (decimal)syllablePerWord - 2.610m * averageWordsPerSentence;
        return Math.Round(Math.Clamp(score, 0m, 100m), 1);
    }

    private static IEnumerable<string> BuildWarnings(
        int wordCount,
        decimal averageWordsPerSentence,
        IReadOnlyList<ReadingQuestionSummary> questions,
        decimal? readability)
    {
        if (wordCount < 80)
            yield return "Metin kısa; güvenilir hız ölçümü için metni amaçlanan süreye göre gözden geçirin.";
        if (averageWordsPerSentence > 20)
            yield return "Ortalama cümle uzunluğu yüksek; cümleleri bölerek okunabilirliği gözden geçirin.";
        if (questions.Count < 3)
            yield return "Kavrama ölçümü için en az üç soru ekleyin.";
        var unscorableQuestionCount = questions.Count(question =>
            !ReadingQuestionQualityRules.HasScorableAnswerKey(question.CorrectAnswer));
        if (unscorableQuestionCount > 0)
            yield return $"{unscorableQuestionCount} sorunun cevap anahtarı eksik veya geçersiz; öğrenciye sunmadan önce düzeltin.";
        if (questions.Count >= 3 && questions.Select(question => question.BloomLevel).Distinct().Count() == 1)
            yield return "Sorular tek bir Bloom seviyesinde; bilişsel kapsamı çeşitlendirmeyi değerlendirin.";
        if (readability is < 30)
            yield return "Okunabilirlik tahmini düşük; hedef yaş ve seviye ile uzman incelemesini doğrulayın.";
    }

    private static IReadOnlyList<ReadingTextQualityDistribution> Distribution(
        IEnumerable<ReadingQuestionSummary> questions,
        Func<ReadingQuestionSummary, int> selector) =>
        questions
            .GroupBy(selector)
            .OrderBy(group => group.Key)
            .Select(group => new ReadingTextQualityDistribution(group.Key, group.Count()))
            .ToArray();

    private static string ReadabilityBand(decimal score) => score switch
    {
        >= 90 => "Çok kolay",
        >= 70 => "Kolay",
        >= 50 => "Orta",
        >= 30 => "Zor",
        _ => "Çok zor"
    };
}
