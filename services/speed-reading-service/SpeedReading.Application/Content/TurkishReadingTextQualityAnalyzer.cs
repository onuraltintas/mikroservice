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
    IReadOnlyList<ReadingTextQualityDistribution> CorrectAnswerDistribution,
    int CorrectAnswerUniqueLongestCount,
    int CorrectAnswerUniqueShortestCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PublicationBlockers);

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
        var validAnswerQuestions = questions
            .Where(question => ReadingQuestionQualityRules.HasScorableAnswerKey(question.CorrectAnswer))
            .ToArray();
        var uniqueLongestCount = validAnswerQuestions.Count(IsCorrectAnswerUniquelyLongest);
        var uniqueShortestCount = validAnswerQuestions.Count(IsCorrectAnswerUniquelyShortest);
        var warnings = BuildWarnings(
            wordCount, averageWordsPerSentence, questions, readability,
            validAnswerQuestions, uniqueLongestCount, uniqueShortestCount).ToArray();
        var publicationBlockers = BuildPublicationBlockers(wordCount, questions, validAnswerQuestions).ToArray();

        return new ReadingTextQualityMetrics(
            wordCount,
            sentenceCount,
            averageWordsPerSentence,
            averageCharactersPerWord,
            readability,
            readability is null ? null : ReadabilityBand(readability.Value),
            Distribution(questions, question => question.BloomLevel),
            Distribution(questions, question => question.DifficultyLevel),
            Distribution(validAnswerQuestions, question => AnswerKeyLevel(question.CorrectAnswer)),
            uniqueLongestCount,
            uniqueShortestCount,
            warnings,
            publicationBlockers);
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
        decimal? readability,
        IReadOnlyList<ReadingQuestionSummary> validAnswerQuestions,
        int uniqueLongestCount,
        int uniqueShortestCount)
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
        if (validAnswerQuestions.Count >= 4
            && validAnswerQuestions.GroupBy(question => question.CorrectAnswer!.Trim(), StringComparer.OrdinalIgnoreCase).Max(group => group.Count()) * 100 >= validAnswerQuestions.Count * 70)
            yield return "Cevap anahtarı aynı seçenekte yoğunlaşıyor; A–D dağılımını dengeleyin.";
        if (validAnswerQuestions.Count >= 4 && uniqueLongestCount * 100 >= validAnswerQuestions.Count * 70)
            yield return "Doğru cevaplar çoğunlukla en uzun seçenek; biçimsel ipucunu kaldırın.";
        if (validAnswerQuestions.Count >= 4 && uniqueShortestCount * 100 >= validAnswerQuestions.Count * 70)
            yield return "Doğru cevaplar çoğunlukla en kısa seçenek; biçimsel ipucunu kaldırın.";
        if (questions.Count >= 3 && questions.Select(question => question.BloomLevel).Distinct().Count() == 1)
            yield return "Sorular tek bir Bloom seviyesinde; bilişsel kapsamı çeşitlendirmeyi değerlendirin.";
        if (readability is < 30)
            yield return "Okunabilirlik tahmini düşük; hedef yaş ve seviye ile uzman incelemesini doğrulayın.";
    }

    private static IEnumerable<string> BuildPublicationBlockers(
        int wordCount,
        IReadOnlyList<ReadingQuestionSummary> questions,
        IReadOnlyList<ReadingQuestionSummary> validAnswerQuestions)
    {
        if (wordCount < 80)
            yield return "Metin 80 kelimenin altında; öğrenci kullanımına açmadan önce genişletin.";
        if (questions.Count < 3)
            yield return "Öğrenci kullanımına açmak için en az üç soru ekleyin.";
        if (validAnswerQuestions.Count != questions.Count)
            yield return "Cevap anahtarı eksik veya geçersiz sorular öğrenci kullanımına açılamaz.";
    }

    private static IReadOnlyList<ReadingTextQualityDistribution> Distribution(
        IEnumerable<ReadingQuestionSummary> questions,
        Func<ReadingQuestionSummary, int> selector) =>
        questions
            .GroupBy(selector)
            .OrderBy(group => group.Key)
            .Select(group => new ReadingTextQualityDistribution(group.Key, group.Count()))
            .ToArray();

    private static int AnswerKeyLevel(string? answerKey) => answerKey?.Trim().ToUpperInvariant() switch
    {
        "A" => 1,
        "B" => 2,
        "C" => 3,
        "D" => 4,
        _ => 0
    };

    private static bool IsCorrectAnswerUniquelyLongest(ReadingQuestionSummary question) =>
        IsCorrectAnswerUniqueExtreme(question, values => values.Max(), (correct, extreme) => correct == extreme);

    private static bool IsCorrectAnswerUniquelyShortest(ReadingQuestionSummary question) =>
        IsCorrectAnswerUniqueExtreme(question, values => values.Min(), (correct, extreme) => correct == extreme);

    private static bool IsCorrectAnswerUniqueExtreme(
        ReadingQuestionSummary question,
        Func<int[], int> selectExtreme,
        Func<int, int, bool> matchesExtreme)
    {
        var lengths = new[] { question.OptionA, question.OptionB, question.OptionC, question.OptionD }
            .Select(option => option.Trim().Length)
            .ToArray();
        var correctIndex = AnswerKeyLevel(question.CorrectAnswer) - 1;
        var extreme = selectExtreme(lengths);
        return correctIndex >= 0
            && matchesExtreme(lengths[correctIndex], extreme)
            && lengths.Count(length => length == extreme) == 1;
    }

    private static string ReadabilityBand(decimal score) => score switch
    {
        >= 90 => "Çok kolay",
        >= 70 => "Kolay",
        >= 50 => "Orta",
        >= 30 => "Zor",
        _ => "Çok zor"
    };
}
