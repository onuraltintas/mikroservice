using SpeedReading.Application.DailyProgress;

namespace SpeedReading.Application.UnitTests;

public sealed class FocusDurationRulesTests
{
    [Theory]
    [InlineData(3000, 32, 2)]
    [InlineData(2800, 40, 2)]
    [InlineData(2600, 48, 3)]
    [InlineData(3000, 48, 3)]
    [InlineData(2800, 56, 3)]
    [InlineData(2500, 48, 2)]
    [InlineData(2500, 56, 3)]
    [InlineData(3000, 56, 3)]
    [InlineData(2900, 48, 3)]
    [InlineData(2600, 56, 3)]
    [InlineData(2700, 64, 3)]
    [InlineData(2700, 48, 3)]
    [InlineData(2700, 56, 3)]
    [InlineData(3100, 56, 3)]
    [InlineData(2800, 64, 3)]
    public void Focus_duration_uses_trial_count_and_cadence_rounded_up(int speedMs, int steps, int minutes)
    {
        var config = System.Text.Json.JsonSerializer.Serialize(new
        {
            engineType = "focus",
            engineConfig = new { speedMs, totalSteps = steps }
        });
        Assert.Equal(minutes, SpeedReadingDailyProgressRules.EstimateFocusDurationMinutes(config));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("invalid JSON")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"engineType\":42}")]
    [InlineData("{\"engineType\":\"focus\",\"engineConfig\":null}")]
    [InlineData("{\"engineType\":\"focus\",\"engineConfig\":[]}")]
    [InlineData("{\"engineType\":\"focus\",\"engineConfig\":{\"speedMs\":\"3000\",\"totalSteps\":32}}")]
    [InlineData("{\"engineType\":\"text_stream\",\"engineConfig\":{\"speedMs\":3000,\"totalSteps\":32}}")]
    [InlineData("{\"engineType\":\"focus\",\"engineConfig\":{\"speedMs\":0,\"totalSteps\":32}}")]
    public void Other_or_incomplete_configuration_keeps_existing_estimate(string configuration)
        => Assert.Equal(5, SpeedReadingDailyProgressRules.EstimateFocusDurationMinutes(configuration));
}
