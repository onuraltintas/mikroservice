using FluentAssertions;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class ReadingPurposeRulesTests
{
    [Theory]
    [InlineData("word_highlight", null, false, "practice")]
    [InlineData("text_fade", null, false, "practice")]
    [InlineData("text_stream", null, false, "practice")]
    [InlineData("regression_reduction", null, false, "practice")]
    [InlineData("subvocalization_reduction", null, false, "practice")]
    [InlineData("free_reading", null, false, "practice")]
    [InlineData("reading_comprehension", null, false, "evaluation")]
    [InlineData("exam_simulation", null, false, "evaluation")]
    [InlineData("adaptive_fluency", "practice", false, "evaluation")]
    [InlineData("word_highlight", "evaluation", false, "evaluation")]
    [InlineData("free_reading", "practice", true, "evaluation")]
    public void Resolves_server_owned_purpose(string engine, string? configured, bool assessment, string expected)
    {
        ExerciseConfigurationRules.ResolveReadingPurpose(engine, configured, assessment).Should().Be(expected);
    }

    [Fact]
    public void Rejects_unknown_reading_purpose()
    {
        var action = () => ExerciseConfigurationRules.ResolveReadingPurpose("word_highlight", "skip", false);
        action.Should().Throw<ArgumentException>();
    }
}
