using FluentAssertions;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class AssessmentPhasePlanCalculatorTests
{
    [Fact]
    public void Makes_baseline_available_when_the_student_has_no_attempts()
    {
        var result = AssessmentPhasePlanCalculator.Calculate([]);

        result.NextPhase.Should().Be(AssessmentAttemptPhase.Baseline);
        result.Phases.Should().ContainSingle(item =>
            item.Phase == AssessmentAttemptPhase.Baseline
            && item.Status == AssessmentPhasePlanStatus.Available
            && item.PrerequisitePhase is null);
        result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining)
            .Status.Should().Be(AssessmentPhasePlanStatus.Locked);
    }

    [Fact]
    public void Unlocks_the_next_phase_after_a_completed_prerequisite()
    {
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var baseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            AssessmentAttemptStatus.Completed,
            completedAt,
            "tr-baseline-v1");

        var result = AssessmentPhasePlanCalculator.Calculate([baseline]);

        var postTraining = result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining);
        postTraining.Status.Should().Be(AssessmentPhasePlanStatus.Available);
        postTraining.PrerequisitePhase.Should().Be(AssessmentAttemptPhase.Baseline);
        postTraining.AvailableAt.Should().Be(completedAt);
        postTraining.FormVersion.Should().Be("tr-posttraining-v1");
        result.NextPhase.Should().Be(AssessmentAttemptPhase.PostTraining);
    }

    [Fact]
    public void Resumes_an_in_progress_phase_before_offering_another_phase()
    {
        var baseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            AssessmentAttemptStatus.Completed,
            DateTime.UtcNow.AddDays(-2),
            "tr-baseline-v1");
        var postTraining = CreateAttempt(
            AssessmentAttemptPhase.PostTraining,
            AssessmentAttemptStatus.InProgress,
            DateTime.UtcNow.AddHours(-1),
            "tr-posttraining-v2");

        var result = AssessmentPhasePlanCalculator.Calculate([baseline, postTraining]);

        var active = result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining);
        active.Status.Should().Be(AssessmentPhasePlanStatus.InProgress);
        active.AttemptId.Should().Be(postTraining.AttemptId);
        active.FormVersion.Should().Be("tr-posttraining-v2");
        result.NextPhase.Should().Be(AssessmentAttemptPhase.PostTraining);
    }

    [Fact]
    public void Uses_the_latest_completed_attempt_for_a_phase_and_keeps_later_phases_locked()
    {
        var firstBaseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            AssessmentAttemptStatus.Completed,
            DateTime.UtcNow.AddDays(-3),
            "tr-baseline-v1");
        var latestBaseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            AssessmentAttemptStatus.Completed,
            DateTime.UtcNow.AddDays(-2),
            "tr-baseline-v2");
        var postTraining = CreateAttempt(
            AssessmentAttemptPhase.PostTraining,
            AssessmentAttemptStatus.Completed,
            DateTime.UtcNow.AddDays(-1),
            "tr-posttraining-v1");

        var result = AssessmentPhasePlanCalculator.Calculate([firstBaseline, latestBaseline, postTraining]);

        var baseline = result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.Baseline);
        baseline.AttemptId.Should().Be(latestBaseline.AttemptId);
        baseline.FormVersion.Should().Be("tr-baseline-v2");
        result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.Retention)
            .Status.Should().Be(AssessmentPhasePlanStatus.Available);
        result.Phases.Single(item => item.Phase == AssessmentAttemptPhase.Transfer)
            .Status.Should().Be(AssessmentPhasePlanStatus.Locked);
        result.NextPhase.Should().Be(AssessmentAttemptPhase.Retention);
    }

    private static AssessmentPhasePlanAttemptInput CreateAttempt(
        AssessmentAttemptPhase phase,
        AssessmentAttemptStatus status,
        DateTime timestamp,
        string formVersion)
    {
        var completedAt = status == AssessmentAttemptStatus.Completed
            ? timestamp.AddMinutes(5)
            : null;
        return new(
            Guid.NewGuid(),
            phase,
            status,
            formVersion,
            timestamp,
            completedAt);
    }
}
