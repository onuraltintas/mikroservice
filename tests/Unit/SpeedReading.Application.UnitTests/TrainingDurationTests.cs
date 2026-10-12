using SpeedReading.Application.DailyProgress;
using Xunit;

namespace SpeedReading.Application.UnitTests;

public sealed class TrainingDurationTests
{
    [Theory]
    [InlineData("{\"engineType\":\"motion_path\",\"engineConfig\":{\"mode\":\"tracking\",\"timing\":{\"durationSeconds\":45}}}", 1)]
    [InlineData("{\"engineType\":\"word_highlight\",\"engineConfig\":{\"pacer\":{\"speedWpm\":100},\"content\":{\"maxWordCount\":300}}}", 3)]
    [InlineData("{\"engineType\":\"free_reading\",\"engineConfig\":{\"timing\":{\"suggestedMaxReadingTimeMs\":420000}}}", 7)]
    [InlineData("{\"engineType\":\"text_fade\",\"engineConfig\":{\"fading\":{\"speedWpm\":60,\"lagMs\":5000},\"content\":{\"maxWordCount\":120}}}", 3)]
    [InlineData("{\"engineType\":\"focus\",\"engineConfig\":{\"speedMs\":3000,\"totalSteps\":32}}", 2)]
    public void Known_timing_uses_configuration_not_five_minutes(string configuration, int minutes)
        => Assert.Equal(minutes, SpeedReadingDailyProgressRules.EstimateDurationMinutes(configuration));

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("bad json")]
    [InlineData("{\"engineType\":\"visual_expansion\",\"engineConfig\":{\"rounds\":50}}")]
    public void Response_dependent_duration_is_not_fabricated(string configuration)
        => Assert.Equal(0, SpeedReadingDailyProgressRules.EstimateDurationMinutes(configuration));
}
