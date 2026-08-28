using FluentAssertions;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingAssessmentAttemptTests
{
    [Fact]
    public void Start_creates_an_in_progress_attempt_with_explicit_phase_and_form()
    {
        var studentId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        var attempt = AssessmentAttempt.Start(
            Guid.NewGuid(),
            studentId,
            AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1",
            "tr-TR",
            ageGroupConfigurationId: null,
            expectedExerciseCount: 3,
            startedAt,
            studentId.ToString());

        attempt.StudentId.Should().Be(studentId);
        attempt.Phase.Should().Be(AssessmentAttemptPhase.Baseline);
        attempt.FormVersion.Should().Be("tr-baseline-v1");
        attempt.Language.Should().Be("tr-TR");
        attempt.ExpectedExerciseCount.Should().Be(3);
        attempt.Status.Should().Be(AssessmentAttemptStatus.InProgress);
        attempt.StartedAt.Should().Be(startedAt);
        attempt.CompletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(AssessmentAttemptPhase.Baseline, "", "tr-TR", 3)]
    [InlineData(AssessmentAttemptPhase.Baseline, "v1", "", 3)]
    [InlineData(AssessmentAttemptPhase.Baseline, "v1", "tr-TR", 0)]
    public void Start_rejects_invalid_measurement_metadata(
        AssessmentAttemptPhase phase,
        string formVersion,
        string language,
        int expectedExerciseCount)
    {
        var act = () => AssessmentAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            phase,
            formVersion,
            language,
            ageGroupConfigurationId: null,
            expectedExerciseCount,
            DateTime.UtcNow,
            null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Complete_is_idempotent_and_records_completion_time()
    {
        var attempt = AssessmentAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AssessmentAttemptPhase.PostTraining,
            "tr-post-v1",
            "tr-TR",
            ageGroupConfigurationId: null,
            expectedExerciseCount: 3,
            DateTime.UtcNow.AddMinutes(-5),
            null);
        var completedAt = DateTime.UtcNow;

        attempt.Complete(completedAt);
        attempt.Complete(completedAt.AddMinutes(1));

        attempt.Status.Should().Be(AssessmentAttemptStatus.Completed);
        attempt.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Abandon_cannot_be_completed_afterward()
    {
        var attempt = AssessmentAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AssessmentAttemptPhase.Retention,
            "tr-retention-v1",
            "tr-TR",
            ageGroupConfigurationId: null,
            expectedExerciseCount: 1,
            DateTime.UtcNow,
            null);

        attempt.Abandon(DateTime.UtcNow);

        var act = () => attempt.Complete(DateTime.UtcNow);

        attempt.Status.Should().Be(AssessmentAttemptStatus.Abandoned);
        act.Should().Throw<InvalidOperationException>();
    }
}
