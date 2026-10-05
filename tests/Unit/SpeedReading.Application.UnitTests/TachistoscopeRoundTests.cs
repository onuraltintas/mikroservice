using FluentAssertions;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class TachistoscopeRoundTests
{
    [Theory]
    [InlineData("number", "2468")]
    [InlineData("letter", "ABC")]
    public void Custom_content_is_used_for_all_content_types(string type, string stimulus)
    {
        var state = new TachistoscopeState { ContentType = type, Source = "custom", Pool = [stimulus] };
        state.SelectStimulus().Should().Be(stimulus);
    }

    [Fact]
    public void Two_consecutive_correct_answers_increase_length_and_shorten_display()
    {
        var state = new TachistoscopeState { DisplayDurationMs = 500, TargetLength = 3, Pool = ["bir", "masa", "kalem"] };
        state.Record("bir", "bir", 100);
        state.Record("bir", "yanlış", 100);
        state.Record("bir", "bir", 100);
        state.TargetLength.Should().Be(3);
        state.Record("bir", "bir", 100);
        state.TargetLength.Should().Be(4);
        state.DisplayDurationMs.Should().Be(450);
    }

    [Fact]
    public void Disabled_adaptation_keeps_explicit_settings()
    {
        var state = new TachistoscopeState { AdaptiveEnabled = false, DisplayDurationMs = 600, TargetLength = 3 };
        for (var i = 0; i < 5; i++) state.Record("İZ", "iz", 100);
        state.DisplayDurationMs.Should().Be(600);
        state.TargetLength.Should().Be(3);
        state.Trials.Should().OnlyContain(item => item.IsCorrect);
    }

    [Fact]
    public void Selection_prefers_the_target_length_without_repeating_the_last_word()
    {
        var state = new TachistoscopeState { TargetLength = 4, LastStimulus = "masa", Pool = ["bir", "masa", "kapı", "kalem"] };
        state.SelectStimulus().Should().Be("kapı");
    }
}
