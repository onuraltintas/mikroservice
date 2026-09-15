using FluentAssertions;
using SpeedReading.Application.Content;

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
}
