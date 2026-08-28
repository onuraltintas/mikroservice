using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

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

    [Fact]
    public void Owned_context_maps_assessment_attempts_and_their_migration()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new OwnedSpeedReadingDbContext(options);

        context.Model.FindEntityType(typeof(AssessmentAttempt)).Should().NotBeNull();
        context.Model.FindEntityType(typeof(AssessmentAttemptExercise)).Should().NotBeNull();
        context.Model.FindEntityType(typeof(ExerciseSession))!
            .FindProperty(nameof(ExerciseSession.AssessmentAttemptId)).Should().NotBeNull();
        context.Model.FindEntityType(typeof(ExerciseSessionResult))!
            .FindProperty(nameof(ExerciseSessionResult.AssessmentAttemptId)).Should().NotBeNull();
        context.Database.GetMigrations()
            .Should()
            .Contain("20260828130000_AddAssessmentMeasurementFoundation")
            .And.Contain("20260828140000_LinkAssessmentAttemptsToSessions")
            .And.Contain("20260828150000_PinAssessmentFormItems");
    }

    [Fact]
    public void Exercise_session_keeps_the_assessment_attempt_reference()
    {
        var assessmentAttemptId = Guid.NewGuid();

        var session = ExerciseSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            readingTextId: null,
            totalSteps: 3,
            DateTime.UtcNow,
            timeLimitSeconds: null,
            assessmentAttemptId: assessmentAttemptId);

        session.AssessmentAttemptId.Should().Be(assessmentAttemptId);
    }

    [Fact]
    public void Exercise_session_result_keeps_the_assessment_attempt_reference()
    {
        var assessmentAttemptId = Guid.NewGuid();

        var result = ExerciseSessionResult.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            readingTextId: null,
            wordsRead: 100,
            timeSpentSeconds: 60,
            rawWpm: 100,
            comprehensionScore: 80,
            weightedKdp: 80,
            score: 80,
            DateTime.UtcNow,
            assessmentAttemptId: assessmentAttemptId);

        result.AssessmentAttemptId.Should().Be(assessmentAttemptId);
    }

    [Fact]
    public void Assessment_attempt_exercise_pins_the_form_item_and_reading_text()
    {
        var attemptId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var readingTextId = Guid.NewGuid();
        var pinnedAt = DateTime.UtcNow;

        var item = AssessmentAttemptExercise.Pin(
            Guid.NewGuid(),
            attemptId,
            exerciseId,
            readingTextId,
            "comprehension",
            1,
            pinnedAt,
            "system");

        item.AssessmentAttemptId.Should().Be(attemptId);
        item.ExerciseId.Should().Be(exerciseId);
        item.ReadingTextId.Should().Be(readingTextId);
        item.Role.Should().Be("comprehension");
        item.OrderIndex.Should().Be(1);
    }
}
