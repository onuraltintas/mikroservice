using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingOwnedDomainTests
{
    [Fact]
    public void Owned_model_uses_a_dedicated_schema_and_normalized_tables()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Model.FindEntityType(typeof(Exercise))!.GetSchema().Should().Be("speed_reading");
        context.Model.FindEntityType(typeof(Exercise))!.GetTableName().Should().Be("exercises");
        context.Model.FindEntityType(typeof(ReadingText))!.GetTableName().Should().Be("reading_texts");
        context.Model.FindEntityType(typeof(ExerciseSession))!.GetTableName().Should().Be("exercise_sessions");
        context.Model.FindEntityType(typeof(ExerciseSessionResult))!.GetTableName().Should().Be("exercise_session_results");
    }

    [Fact]
    public void Owned_model_enforces_one_result_per_session()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var hasUniqueSessionIndex = context.Model
            .FindEntityType(typeof(ExerciseSessionResult))!
            .GetIndexes()
            .Any(item => item.IsUnique
                && item.Properties.Count == 1
                && item.Properties.Single().Name == nameof(ExerciseSessionResult.SessionId));

        hasUniqueSessionIndex.Should().BeTrue();
    }

    [Fact]
    public void Owned_create_script_does_not_reference_legacy_tables()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var script = context.Database.GenerateCreateScript();

        script.Should().Contain("speed_reading");
        script.Should().Contain("reading_questions");
        script.Should().Contain("exercise_session_results");
        script.Should().NotContain("ContentBlocks");
        script.Should().NotContain("Legacy");
    }

    [Fact]
    public void Owned_context_discovers_only_its_owned_migration()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Database.GetMigrations()
            .Should()
            .ContainSingle("20260827110000_CreateOwnedSpeedReadingCore");
    }

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
