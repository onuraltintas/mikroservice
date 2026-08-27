using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingOwnedDomainTests
{
    [Fact]
    public void Exercise_requires_a_title_and_type_code()
    {
        var act = () => Exercise.Create(
            title: " ",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Session_starts_active_with_zero_progress()
    {
        var session = ExerciseSession.Start(
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: null,
            totalSteps: 10,
            startedAt: DateTime.UtcNow,
            timeLimitSeconds: 120);

        session.Status.Should().Be(ExerciseSessionStatus.Active);
        session.CurrentStep.Should().Be(0);
        session.TotalSteps.Should().Be(10);
        session.CorrectCount.Should().Be(0);
        session.IncorrectCount.Should().Be(0);
    }

    [Fact]
    public void Session_rejects_the_same_question_twice()
    {
        var session = ExerciseSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            totalSteps: 2,
            DateTime.UtcNow,
            timeLimitSeconds: null);
        var questionId = Guid.NewGuid();

        session.RecordAnswer(questionId, "A", isCorrect: true, timeSpentSeconds: 3, bloomLevel: 2);

        var act = () => session.RecordAnswer(questionId, "A", isCorrect: true, 3, 2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pausing_and_resuming_accumulates_paused_seconds()
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-2);
        var pausedAt = startedAt.AddSeconds(30);
        var resumedAt = pausedAt.AddSeconds(20);
        var session = ExerciseSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            totalSteps: 1,
            startedAt,
            timeLimitSeconds: null);

        session.Pause(pausedAt);
        session.Resume(resumedAt);

        session.Status.Should().Be(ExerciseSessionStatus.Active);
        session.TotalPausedSeconds.Should().Be(20);
        session.PausedAt.Should().BeNull();
    }
}
