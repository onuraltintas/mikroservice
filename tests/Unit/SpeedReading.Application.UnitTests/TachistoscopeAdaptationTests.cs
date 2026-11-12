using FluentAssertions;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// November 2026 rule: independent blocks of three scored trials. 3/3 correct shortens the display
/// by 15 %, 2/3 keeps it, 0–1 lengthens it by 15 %. At the duration bounds the length changes instead
/// and the duration returns to its start; duration and length never both get harder in one block.
/// </summary>
public sealed class TachistoscopeAdaptationTests
{
    private static TachistoscopeState Create(int duration = 640, int min = 250, int max = 1500,
        int length = 6, int minLength = 4, int maxLength = 8) => new()
    {
        ContentType = "word", DisplayDurationMs = duration, InitialDurationMs = duration,
        MinDurationMs = min, MaxDurationMs = max, TargetLength = length, InitialTargetLength = length,
        MinTargetLength = minLength, MaxTargetLength = maxLength
    };

    private static void Block(TachistoscopeState state, int correct)
    {
        for (var i = 0; i < 3; i++) state.Record("kalem", i < correct ? "kalem" : "yanlış", 100);
    }

    [Theory]
    [InlineData(3, 544)]
    [InlineData(2, 640)]
    [InlineData(1, 736)]
    [InlineData(0, 736)]
    public void A_three_trial_block_adjusts_duration_by_fifteen_percent(int correct, int expected)
    {
        var state = Create();
        Block(state, correct);
        state.DisplayDurationMs.Should().Be(expected);
        state.TargetLength.Should().Be(6);
        state.Trials.Should().OnlyContain(trial => trial.DisplayDurationMs == 640);
    }

    [Fact]
    public void Blocks_are_independent_and_an_incomplete_final_block_changes_nothing()
    {
        var state = Create();
        Block(state, 3);
        state.DisplayDurationMs.Should().Be(544);
        state.Record("kalem", "kalem", 100);
        state.Record("kalem", "kalem", 100);
        state.DisplayDurationMs.Should().Be(544, "two trials are not a complete block");
        state.Record("kalem", "yanlış", 100);
        state.DisplayDurationMs.Should().Be(544, "the second block has 2/3 correct");
        Block(state, 1);
        state.DisplayDurationMs.Should().Be(626);
        state.Record("kalem", "kalem", 100);
        state.DisplayDurationMs.Should().Be(626);
        state.TargetLength.Should().Be(6);
    }

    [Fact]
    public void Success_at_the_minimum_duration_lengthens_the_stimulus_and_resets_the_duration()
    {
        var state = Create(duration: 640);
        state.DisplayDurationMs = 250;
        Block(state, 3);
        state.TargetLength.Should().Be(7);
        state.DisplayDurationMs.Should().Be(640, "duration and length are never both made harder in one block");
    }

    [Fact]
    public void Failure_at_the_maximum_duration_shortens_the_stimulus_and_resets_the_duration()
    {
        var state = Create(duration: 640);
        state.DisplayDurationMs = 1500;
        Block(state, 1);
        state.TargetLength.Should().Be(5);
        state.DisplayDurationMs.Should().Be(640);
    }

    [Fact]
    public void Duration_and_length_stay_within_their_ranges()
    {
        var state = Create(duration: 400, min: 300, max: 500, length: 7, minLength: 6, maxLength: 7);
        for (var block = 0; block < 20; block++)
        {
            Block(state, 3);
            state.DisplayDurationMs.Should().BeInRange(300, 500);
            state.TargetLength.Should().BeInRange(6, 7);
        }
        state.DisplayDurationMs.Should().Be(300);
        state.TargetLength.Should().Be(7);
        for (var block = 0; block < 20; block++)
        {
            Block(state, 0);
            state.DisplayDurationMs.Should().BeInRange(300, 500);
            state.TargetLength.Should().BeInRange(6, 7);
        }
        state.DisplayDurationMs.Should().Be(500);
        state.TargetLength.Should().Be(6);
    }

    [Fact]
    public void A_shortened_duration_is_clamped_to_the_minimum_without_changing_length()
    {
        var state = Create(duration: 280, min: 250);
        Block(state, 3);
        state.DisplayDurationMs.Should().Be(250);
        state.TargetLength.Should().Be(6);
        var slow = Create(duration: 1400, max: 1500);
        Block(slow, 0);
        slow.DisplayDurationMs.Should().Be(1500);
        slow.TargetLength.Should().Be(6);
    }

    [Fact]
    public void Assessment_rounds_follow_the_fixed_protocol_without_adaptation()
    {
        var state = Create(duration: 600);
        state.AdaptiveEnabled = false;
        state.AssessmentProtocol = new AssessmentBlockProtocol
            { PracticeRounds = 3, RoundsPerBlock = 6, BlockValues = [600, 450, 300] };
        for (var round = 0; round < 8; round++)
        {
            state.Record("123", "123", 100);
            state.DisplayDurationMs.Should().Be(600, "three practice rounds and the first block use the first value");
        }
        state.Record("123", "123", 100);
        state.DisplayDurationMs.Should().Be(450);
        for (var round = 0; round < 6; round++) state.Record("123", "123", 100);
        state.DisplayDurationMs.Should().Be(300);
        state.TargetLength.Should().Be(6);
    }

    [Fact]
    public void Practice_trials_do_not_count_towards_a_block()
    {
        var state = Create();
        state.AssessmentProtocol = null;
        state.Trials.Add(new TachistoscopeTrial("kalem", "kalem", true, 100, 640, IsPractice: true));
        state.Round = 1;
        state.Record("kalem", "kalem", 100);
        state.Record("kalem", "kalem", 100);
        state.DisplayDurationMs.Should().Be(640);
        state.Record("kalem", "kalem", 100);
        state.DisplayDurationMs.Should().Be(544);
    }

    [Fact]
    public void Disabled_adaptation_keeps_the_configured_values()
    {
        var state = Create();
        state.AdaptiveEnabled = false;
        for (var block = 0; block < 4; block++) Block(state, 3);
        state.DisplayDurationMs.Should().Be(640);
        state.TargetLength.Should().Be(6);
    }
}
