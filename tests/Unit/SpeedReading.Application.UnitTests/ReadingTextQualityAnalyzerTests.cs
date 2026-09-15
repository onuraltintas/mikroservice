using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class ReadingTextQualityAnalyzerTests
{
    [Fact]
    public void Analyze_returns_turkish_readability_and_flags_insufficient_questions()
    {
        var result = TurkishReadingTextQualityAnalyzer.Analyze(
            "Ali topu attı. Ece kitabı okudu. Herkes dikkatle dinledi.",
            "tr",
            []);

        result.WordCount.Should().Be(9);
        result.SentenceCount.Should().Be(3);
        result.EstimatedAtesmanReadability.Should().NotBeNull();
        result.EstimatedAtesmanReadability!.Value.Should().BeInRange(0, 100);
        result.Warnings.Should().Contain(warning => warning.Contains("en az üç soru", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_reports_question_bloom_and_difficulty_distribution()
    {
        var questions = new[]
        {
            Question("Ana fikir nedir?", bloomLevel: 1, difficultyLevel: 1),
            Question("Neden-sonuç ilişkisini bulun.", bloomLevel: 2, difficultyLevel: 2),
            Question("Metni değerlendirin.", bloomLevel: 2, difficultyLevel: 3)
        };

        var result = TurkishReadingTextQualityAnalyzer.Analyze(
            "Bu metin yeterli sayıda kelime içerir. Her cümle anlaşılır ve kısa tutulur.",
            "tr",
            questions);

        result.BloomDistribution.Should().BeEquivalentTo(
            [new ReadingTextQualityDistribution(1, 1), new ReadingTextQualityDistribution(2, 2)]);
        result.DifficultyDistribution.Should().BeEquivalentTo(
            [new ReadingTextQualityDistribution(1, 1), new ReadingTextQualityDistribution(2, 1), new ReadingTextQualityDistribution(3, 1)]);
        result.Warnings.Should().NotContain(warning => warning.Contains("en az üç soru", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_flags_answer_key_and_option_length_patterns_that_make_answers_guessable()
    {
        var questions = new[]
        {
            Question("Soru 1", bloomLevel: 1, difficultyLevel: 1, correctAnswer: "A", optionA: "Bu seçenek diğer seçeneklerden belirgin biçimde daha uzundur.", optionB: "Kısa", optionC: "Kısa", optionD: "Kısa"),
            Question("Soru 2", bloomLevel: 2, difficultyLevel: 2, correctAnswer: "A", optionA: "Bu seçenek diğer seçeneklerden belirgin biçimde daha uzundur.", optionB: "Kısa", optionC: "Kısa", optionD: "Kısa"),
            Question("Soru 3", bloomLevel: 3, difficultyLevel: 3, correctAnswer: "A", optionA: "Bu seçenek diğer seçeneklerden belirgin biçimde daha uzundur.", optionB: "Kısa", optionC: "Kısa", optionD: "Kısa"),
            Question("Soru 4", bloomLevel: 1, difficultyLevel: 1, correctAnswer: "A", optionA: "Bu seçenek diğer seçeneklerden belirgin biçimde daha uzundur.", optionB: "Kısa", optionC: "Kısa", optionD: "Kısa")
        };

        var result = TurkishReadingTextQualityAnalyzer.Analyze("Yeterli uzunlukta bir metin. İkinci cümle örnek sağlar.", "tr", questions);

        result.CorrectAnswerDistribution.Should().BeEquivalentTo([new ReadingTextQualityDistribution(1, 4)]);
        result.Warnings.Should().Contain(warning => warning.Contains("cevap anahtarı", StringComparison.OrdinalIgnoreCase));
        result.Warnings.Should().Contain(warning => warning.Contains("en uzun", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_reports_publish_blockers_for_a_short_text_without_enough_questions()
    {
        var result = TurkishReadingTextQualityAnalyzer.Analyze(
            "Kısa bir metin.",
            "tr",
            []);

        result.PublicationBlockers.Should().Contain(blocker =>
            blocker.Contains("80", StringComparison.OrdinalIgnoreCase));
        result.PublicationBlockers.Should().Contain(blocker =>
            blocker.Contains("en az üç soru", StringComparison.OrdinalIgnoreCase));
    }

    private static ReadingQuestionSummary Question(
        string text,
        int bloomLevel,
        int difficultyLevel,
        string correctAnswer = "A",
        string optionA = "A",
        string optionB = "B",
        string optionC = "C",
        string optionD = "D") =>
        new(Guid.NewGuid(), text, 1, bloomLevel, difficultyLevel, null, optionA, optionB, optionC, optionD, correctAnswer, 1);
}
