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

    private static ReadingQuestionSummary Question(string text, int bloomLevel, int difficultyLevel) =>
        new(Guid.NewGuid(), text, 1, bloomLevel, difficultyLevel, null, "A", "B", "C", "D", "A", 1);
}
