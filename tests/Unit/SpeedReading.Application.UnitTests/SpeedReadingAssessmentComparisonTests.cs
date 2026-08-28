using FluentAssertions;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingAssessmentComparisonTests
{
    [Fact]
    public void Calculates_post_training_deltas_against_the_completed_baseline()
    {
        var baseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1",
            DateTime.UtcNow.AddDays(-7),
            new AssessmentComparisonResultInput(true, 200, 80, 75, "comprehension"));
        var postTraining = CreateAttempt(
            AssessmentAttemptPhase.PostTraining,
            "tr-posttraining-v1",
            DateTime.UtcNow,
            new AssessmentComparisonResultInput(true, 250, 90, 85, "comprehension"));

        var result = AssessmentComparisonCalculator.Calculate([postTraining, baseline]);

        result.Should().HaveCount(2);
        result[0].Phase.Should().Be(AssessmentAttemptPhase.Baseline);
        result[0].WpmDeltaFromBaseline.Should().BeNull();
        result[1].Phase.Should().Be(AssessmentAttemptPhase.PostTraining);
        result[1].AverageWpm.Should().Be(250);
        result[1].AverageComprehension.Should().Be(90);
        result[1].WpmDeltaFromBaseline.Should().Be(50);
        result[1].ComprehensionDeltaFromBaseline.Should().Be(10);
    }

    [Fact]
    public void Ignores_unmeasured_results_and_leaves_unavailable_metrics_null()
    {
        var attempt = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1",
            DateTime.UtcNow,
            new AssessmentComparisonResultInput(false, null, null, null, "comprehension"));

        var result = AssessmentComparisonCalculator.Calculate([attempt]);

        result.Should().ContainSingle();
        result[0].CompletedExerciseCount.Should().Be(0);
        result[0].AverageWpm.Should().BeNull();
        result[0].AverageComprehension.Should().BeNull();
        result[0].AverageScore.Should().BeNull();
    }

    [Fact]
    public void Does_not_use_an_in_progress_attempt_as_the_baseline()
    {
        var inProgressBaseline = CreateAttempt(
            AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1",
            DateTime.UtcNow.AddDays(-1),
            new AssessmentComparisonResultInput(true, 200, 80, 75, "comprehension"),
            AssessmentAttemptStatus.InProgress);
        var postTraining = CreateAttempt(
            AssessmentAttemptPhase.PostTraining,
            "tr-posttraining-v1",
            DateTime.UtcNow,
            new AssessmentComparisonResultInput(true, 250, 90, 85, "comprehension"));

        var result = AssessmentComparisonCalculator.Calculate([inProgressBaseline, postTraining]);

        result.Should().ContainSingle();
        result[0].Phase.Should().Be(AssessmentAttemptPhase.PostTraining);
        result[0].WpmDeltaFromBaseline.Should().BeNull();
    }

    private static AssessmentComparisonAttemptInput CreateAttempt(
        AssessmentAttemptPhase phase,
        string formVersion,
        DateTime startedAt,
        AssessmentComparisonResultInput result,
        AssessmentAttemptStatus status = AssessmentAttemptStatus.Completed) =>
        new(
            Guid.NewGuid(),
            phase,
            status,
            formVersion,
            startedAt,
            startedAt.AddMinutes(5),
            1,
            [result]);
}
