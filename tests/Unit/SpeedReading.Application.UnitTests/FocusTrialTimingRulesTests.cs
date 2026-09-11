using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class FocusTrialTimingRulesTests
{
    [Fact]
    public void Presentation_window_uses_server_time_and_excludes_paused_seconds()
    {
        var startedAt = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

        FocusTrialTimingRules.ExpectedIndex(startedAt, startedAt.AddSeconds(7), 2, 1000, 10)
            .Should().Be(5);
        FocusTrialTimingRules.CanPresent(requestedIndex: 5, presentedIndex: 4, expectedIndex: 5)
            .Should().BeTrue();
        FocusTrialTimingRules.CanPresent(requestedIndex: 6, presentedIndex: 4, expectedIndex: 5)
            .Should().BeFalse();
    }

    [Fact]
    public void Assessment_response_window_excludes_pause_after_presentation()
    {
        var presentedAt = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

        FocusTrialTimingRules.IsAssessmentResponseOnTime(
            presentedAt,
            presentedAt.AddSeconds(7),
            pausedSecondsSincePresentation: 5,
            speedMilliseconds: 1000).Should().BeTrue();
        FocusTrialTimingRules.IsAssessmentResponseOnTime(
            presentedAt,
            presentedAt.AddSeconds(8),
            pausedSecondsSincePresentation: 5,
            speedMilliseconds: 1000).Should().BeFalse();
    }

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(2, 3, false)]
    [InlineData(4, 3, false)]
    public void Assessment_response_must_target_the_server_presented_trial(
        int requestedIndex,
        int presentedIndex,
        bool expected)
    {
        FocusTrialTimingRules.IsCurrentTrial(requestedIndex, presentedIndex).Should().Be(expected);
    }
}
