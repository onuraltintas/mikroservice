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
        context.Model.FindEntityType(typeof(ExerciseTypeCategory))!.GetTableName().Should().Be("exercise_type_categories");
        context.Model.FindEntityType(typeof(ExerciseType))!.GetTableName().Should().Be("exercise_types");
        context.Model.FindEntityType(typeof(Exercise))!.GetTableName().Should().Be("exercises");
        context.Model.FindEntityType(typeof(ReadingText))!.GetTableName().Should().Be("reading_texts");
        context.Model.FindEntityType(typeof(ExerciseSession))!.GetTableName().Should().Be("exercise_sessions");
        context.Model.FindEntityType(typeof(ExerciseSessionResult))!.GetTableName().Should().Be("exercise_session_results");
        context.Model.FindEntityType(typeof(ReadingSession))!.GetTableName().Should().Be("reading_sessions");
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
        script.Should().NotContain("speed_reading.legacy_");
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
            .Contain("20260827110000_CreateOwnedSpeedReadingCore")
            .And.Contain("20260827120000_AddOwnedReadingSessionHistory");
    }

    [Fact]
    public void Exercise_requires_a_title_and_type_code()
    {
        var exerciseTypeId = Guid.NewGuid();
        var act = () => Exercise.Create(
            title: " ",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId: Guid.NewGuid(),
            exerciseTypeId: exerciseTypeId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Exercise_keeps_the_owned_exercise_type_reference()
    {
        var exerciseTypeId = Guid.NewGuid();
        var exercise = Exercise.Create(
            title: "Hızlı okuma",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId: Guid.NewGuid(),
            exerciseTypeId: exerciseTypeId);

        exercise.ExerciseTypeId.Should().Be(exerciseTypeId);
    }

    [Fact]
    public void Imported_catalog_entities_keep_source_identity_and_audit_metadata()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var creatorId = Guid.NewGuid();

        var exercise = Exercise.Import(
            id,
            title: "Hızlı okuma",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId,
            exerciseTypeId: Guid.NewGuid(),
            createdAt: createdAt,
            targetAgeGroupId: null,
            description: null,
            isActive: true,
            createdBy: creatorId.ToString(),
            updatedAt: null,
            updatedBy: null);

        exercise.Id.Should().Be(id);
        exercise.CreatedAt.Should().Be(createdAt);
        exercise.CreatedBy.Should().Be(creatorId.ToString());
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
    public void Imported_session_preserves_completed_state_and_answers()
    {
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var session = ExerciseSession.Import(
            id: sessionId,
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: Guid.NewGuid(),
            studentAssignmentId: null,
            status: ExerciseSessionStatus.Completed,
            startTime: DateTime.UtcNow.AddMinutes(-3),
            endTime: DateTime.UtcNow,
            totalPausedSeconds: 5,
            pausedAt: null,
            timeLimitSeconds: 120,
            currentStep: 2,
            totalSteps: 2,
            correctCount: 1,
            incorrectCount: 1,
            sessionDataJson: "{}",
            customDataJson: null,
            processedActionsJson: "{}",
            createdAt: DateTime.UtcNow.AddDays(-1),
            createdBy: null,
            updatedAt: null,
            updatedBy: null);

        session.ImportAnswer(ExerciseSessionAnswer.Import(
            Guid.NewGuid(), sessionId, questionId, "A", true, 2, 1));

        session.Status.Should().Be(ExerciseSessionStatus.Completed);
        session.CurrentStep.Should().Be(2);
        session.Answers.Should().ContainSingle(item => item.QuestionId == questionId);
    }

    [Fact]
    public void Imported_result_can_keep_a_missing_legacy_session_reference()
    {
        var result = ExerciseSessionResult.Import(
            id: Guid.NewGuid(),
            sessionId: null,
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: null,
            wordsRead: 100,
            timeSpentSeconds: 60,
            rawWpm: 100,
            comprehensionScore: 80,
            weightedKdp: 80,
            score: 80,
            completedAt: DateTime.UtcNow,
            questionAnswersJson: "[]",
            readingMovementsJson: "[]",
            legacySessionId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            createdBy: null,
            updatedAt: null,
            updatedBy: null);

        result.SessionId.Should().BeNull();
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
