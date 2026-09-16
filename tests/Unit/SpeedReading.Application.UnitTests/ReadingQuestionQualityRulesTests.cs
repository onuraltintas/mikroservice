using FluentAssertions;
using SpeedReading.Application.Content;
using SpeedReading.Application.QuestionBank;

namespace SpeedReading.Application.UnitTests;

public sealed class ReadingQuestionQualityRulesTests
{
    [Theory]
    [InlineData("A")]
    [InlineData(" b ")]
    [InlineData("c")]
    [InlineData("D")]
    public void Accepts_only_a_through_d_as_scorable_answer_keys(string answerKey)
    {
        ReadingQuestionQualityRules.HasScorableAnswerKey(answerKey).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E")]
    [InlineData("A/B")]
    public void Rejects_missing_or_invalid_answer_keys(string answerKey)
    {
        ReadingQuestionQualityRules.HasScorableAnswerKey(answerKey).Should().BeFalse();
    }

    [Fact]
    public void Warns_when_the_correct_option_is_a_distinct_length_outlier()
    {
        var request = new ExamQuestionRequest(
            "Bu metin, öğrencinin ana düşünceyi ve verilen bilgileri birlikte değerlendirmesini gerektiren yeterli bir bağlam sunar.",
            "Metne göre temel sonuç nedir?",
            "Kısa bir sonuç.",
            "Bu sonuç, metindeki neden ve etkileri birlikte açıklayan belirgin bir değerlendirmedir.",
            "Başka bir sonuç.",
            "İlgisiz bir sonuç.",
            null,
            "B",
            1,
            2,
            20,
            "Genel",
            1);

        ExamQuestionQualityAnalyzer.Analyze(request).Warnings
            .Should().ContainSingle(item => item.Code == "correct-option-length-cue");
    }

    [Fact]
    public void Does_not_warn_for_balanced_option_lengths()
    {
        var request = new ExamQuestionRequest(
            "Bu metin, öğrencinin ana düşünceyi ve verilen bilgileri birlikte değerlendirmesini gerektiren yeterli bir bağlam sunar.",
            "Metne göre temel sonuç nedir?",
            "İş birliği önemlidir.",
            "Düzenli çalışma önemlidir.",
            "Açık iletişim önemlidir.",
            "Dikkatli planlama önemlidir.",
            null,
            "B",
            1,
            2,
            20,
            "Genel",
            1);

        ExamQuestionQualityAnalyzer.Analyze(request).Warnings
            .Should().NotContain(item => item.Code == "correct-option-length-cue");
    }
}
