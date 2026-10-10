using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class TachistoscopeSettingsTests
{
    [Theory]
    [InlineData("number", 3, 12)]
    [InlineData("word", 4, 6)]
    public void Resuming_legacy_state_preserves_its_original_length_bounds(string contentType, int initialLength, int expectedMaximum)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { tachistoscope = new {
            contentType, initialTargetLength = initialLength, targetLength = initialLength,
            pool = new[] { "masa", "kitap", "kelime" } } });
        var service = typeof(SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var restored = service.GetMethod("DeserializeState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [json])!;
        var state = (TachistoscopeState)restored.GetType().GetProperty("Tachistoscope")!.GetValue(restored)!;
        Assert.Equal(initialLength, state.MinTargetLength);
        Assert.Equal(expectedMaximum, state.MaxTargetLength);
    }
    [Theory]
    [InlineData("{\"content\":{\"length\":0}}")]
    [InlineData("{\"content\":{\"length\":13}}")]
    [InlineData("{\"adaptive\":{\"minLength\":0}}")]
    [InlineData("{\"adaptive\":{\"maxLength\":13}}")]
    [InlineData("{\"adaptive\":{\"minLength\":5,\"maxLength\":3}}")]
    [InlineData("{\"content\":{\"length\":6},\"adaptive\":{\"minLength\":3,\"maxLength\":5}}")]
    public void Invalid_length_settings_are_rejected(string settings)
    {
        var config = "{\"engineType\":\"text_stream\",\"engineConfig\":" + settings + "}";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(config, "text_stream"));
    }

    [Theory]
    [InlineData("{\"content\":{\"length\":3},\"engineConfig\":{\"content\":{\"length\":4}}}")]
    [InlineData("{\"adaptive\":{\"minLength\":5},\"engineConfig\":{\"adaptive\":{\"maxLength\":3}}}")]
    public void Conflicting_cross_layer_length_settings_are_rejected(string settings)
    {
        var config = settings.Insert(1, "\"engineType\":\"text_stream\",");
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(config, "text_stream"));
    }

    [Theory]
    [InlineData(3, 5, 400, 2000)]
    [InlineData(3, 6, 300, 1500)]
    [InlineData(4, 6, 250, 1500)]
    [InlineData(4, 7, 250, 1500)]
    public void Success_and_failure_sequences_never_escape_age_bounds(int minLength, int maxLength, int minDuration, int maxDuration)
    {
        var state = new TachistoscopeState { ContentType = "number", MinTargetLength = minLength,
            MaxTargetLength = maxLength, TargetLength = minLength, MinDurationMs = minDuration,
            MaxDurationMs = maxDuration, DisplayDurationMs = minDuration, InitialDurationMs = 800 };
        for (var i = 0; i < 600; i++)
        {
            var stimulus = state.SelectStimulus();
            Assert.InRange(stimulus.Length, minLength, maxLength);
            state.Record(stimulus, stimulus, 100);
            Assert.InRange(state.DisplayDurationMs, minDuration, maxDuration);
        }
        Assert.Equal(maxLength, state.TargetLength);
        for (var i = 0; i < 600; i++) state.Record("1234", "wrong", 100);
        Assert.Equal(minLength, state.TargetLength);
        Assert.Equal(maxDuration, state.DisplayDurationMs);
    }

    [Fact]
    public void Length_changes_only_after_success_at_floor_or_failure_at_ceiling()
    {
        var state = new TachistoscopeState { MinTargetLength = 3, MaxTargetLength = 5,
            TargetLength = 4, MinDurationMs = 400, MaxDurationMs = 2000,
            DisplayDurationMs = 400, InitialDurationMs = 1100 };
        for (var i = 0; i < 5; i++) state.Record("1234", "1234", 100);
        Assert.Equal(5, state.TargetLength);
        Assert.Equal(1100, state.DisplayDurationMs);
        Assert.Equal(400, state.Trials[4].DisplayDurationMs);
        state.DisplayDurationMs = 2000;
        for (var i = 0; i < 5; i++) state.Record("12345", "wrong", 100);
        Assert.Equal(4, state.TargetLength);
        Assert.Equal(1100, state.DisplayDurationMs);
        Assert.Equal(2000, state.Trials[9].DisplayDurationMs);
    }
}
