using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SpeedReading.Application.Analytics;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ReadingSessionAnswerTests
{
    [Fact]
    public void Import_preserves_question_snapshot_and_selected_answer()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var completedAt = new DateTime(2026, 9, 16, 12, 30, 0, DateTimeKind.Utc);

        var answer = ReadingSessionAnswer.Import(
            id,
            sessionId,
            questionId,
            questionType: 2,
            bloomLevel: 4,
            orderIndex: 3,
            selectedAnswer: " B ",
            isCorrect: true,
            completedAt,
            createdBy: "student-1");

        answer.Id.Should().Be(id);
        answer.SessionId.Should().Be(sessionId);
        answer.QuestionId.Should().Be(questionId);
        answer.QuestionType.Should().Be(2);
        answer.BloomLevel.Should().Be(4);
        answer.OrderIndex.Should().Be(3);
        answer.SelectedAnswer.Should().Be("B");
        answer.IsCorrect.Should().BeTrue();
        answer.CreatedAt.Should().Be(completedAt);
        answer.CreatedBy.Should().Be("student-1");
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(4, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 7, 0)]
    [InlineData(1, 1, -1)]
    public void Import_rejects_invalid_question_metadata(int questionType, int bloomLevel, int orderIndex)
    {
        var act = () => ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            questionType,
            bloomLevel,
            orderIndex,
            "A",
            false,
            DateTime.UtcNow,
            "student-1");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Import_rejects_an_option_label_instead_of_an_option_key()
    {
        var act = () => ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            1,
            0,
            "Doğru seçenek",
            true,
            DateTime.UtcNow,
            "student-1");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Import_normalizes_an_empty_timeout_answer_to_the_timeout_sentinel()
    {
        var answer = ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            questionType: 1,
            bloomLevel: 1,
            orderIndex: 0,
            selectedAnswer: " ",
            isCorrect: false,
            completedAt: DateTime.UtcNow,
            createdBy: "student-1");

        answer.SelectedAnswer.Should().Be(ReadingSessionAnswer.TimeoutAnswer.ToUpperInvariant());
    }

    [Fact]
    public void Question_type_summary_returns_localized_labels_and_accuracy()
    {
        var result = ReadingQuestionAnalyticsRules.SummarizeQuestionTypes([
            new ReadingQuestionTypeAggregate(3, 4, 1),
            new ReadingQuestionTypeAggregate(1, 4, 3),
            new ReadingQuestionTypeAggregate(2, 2, 2)
        ]);

        result.Should().HaveCount(3);
        result[0].Should().Be(new StudentAnalyticsQuestionTypePoint("Gerçek Anlam", 75, 4, 3));
        result[1].Should().Be(new StudentAnalyticsQuestionTypePoint("Çıkarım", 100, 2, 2));
        result[2].Should().Be(new StudentAnalyticsQuestionTypePoint("Değerlendirme", 25, 4, 1));
    }

    [Fact]
    public void Bloom_summary_returns_ordered_levels_and_accuracy()
    {
        var result = ReadingQuestionAnalyticsRules.SummarizeBloomLevels([
            new ReadingQuestionBloomAggregate(4, 4, 1),
            new ReadingQuestionBloomAggregate(2, 2, 2),
            new ReadingQuestionBloomAggregate(4, 1, 1),
            new ReadingQuestionBloomAggregate(0, 5, 5)
        ]);

        result.Should().HaveCount(2);
        result[0].Should().Be(new StudentAnalyticsBloomLevelPoint(2, "Anlama", 100, 2, 2));
        result[1].Should().Be(new StudentAnalyticsBloomLevelPoint(4, "Analiz", 40, 5, 2));
    }

    [Fact]
    public void Admin_question_analytics_returns_type_and_bloom_breakdowns()
    {
        var result = AdminStudentReadingQuestionAnalyticsCalculator.Calculate([
            new AdminReadingQuestionAnswerSample(1, 2, true),
            new AdminReadingQuestionAnswerSample(1, 2, false),
            new AdminReadingQuestionAnswerSample(2, 4, true)
        ]);

        result.DataAvailable.Should().BeTrue();
        result.TotalQuestionsAttempted.Should().Be(3);
        result.CorrectAnswers.Should().Be(2);
        result.SuccessRate.Should().Be(66.67m);
        result.QuestionTypes.Should().BeEquivalentTo([
            new AdminReadingQuestionTypePerformance("Gerçek Anlam", 2, 1, 50),
            new AdminReadingQuestionTypePerformance("Çıkarım", 1, 1, 100)
        ], options => options.WithStrictOrdering());
        result.BloomLevels.Should().BeEquivalentTo([
            new AdminReadingBloomLevelPerformance(2, "Anlama", 2, 1, 50),
            new AdminReadingBloomLevelPerformance(4, "Analiz", 1, 1, 100)
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void Admin_question_analytics_explains_when_no_answers_are_available()
    {
        var result = AdminStudentReadingQuestionAnalyticsCalculator.Calculate([]);

        result.DataAvailable.Should().BeFalse();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
        result.QuestionTypes.Should().BeEmpty();
        result.BloomLevels.Should().BeEmpty();
    }

    [Fact]
    public void Owned_model_maps_reading_session_answers_with_unique_session_question_key()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var entity = context.Model.FindEntityType(typeof(ReadingSessionAnswer))!;
        var table = StoreObjectIdentifier.Table("reading_session_answers", "speed_reading");

        entity.GetTableName().Should().Be("reading_session_answers");
        entity.FindProperty(nameof(ReadingSessionAnswer.SelectedAnswer))!
            .GetColumnName(table).Should().Be("selected_answer");
        entity.FindProperty(nameof(ReadingSessionAnswer.QuestionType))!
            .GetColumnName(table).Should().Be("question_type");
        entity.GetIndexes().Should().ContainSingle(index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ReadingSessionAnswer.SessionId), nameof(ReadingSessionAnswer.QuestionId) }));

        var sessionEntity = context.Model.FindEntityType(typeof(ReadingSession))!;
        sessionEntity.FindProperty(nameof(ReadingSession.IsMeasured))!
            .GetColumnName(StoreObjectIdentifier.Table("reading_sessions", "speed_reading"))
            .Should().Be("is_measured");
    }

    [Fact]
    public void Reading_session_preserves_measurement_status()
    {
        var session = ReadingSession.Import(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            readingTimeSeconds: 1,
            calculatedWpm: 0,
            correctAnswers: 1,
            totalQuestions: 1,
            comprehensionRate: 100,
            efficiencyScore: 0,
            completedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            createdBy: "student-1",
            updatedAt: null,
            updatedBy: null,
            isMeasured: false);

        session.IsMeasured.Should().BeFalse();
    }

    [Fact]
    public void Student_reading_attempt_preserves_the_word_count_snapshot()
    {
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        attempt.SetWordCountSnapshot(125, DateTime.UtcNow);

        attempt.WordCountSnapshot.Should().Be(125);
    }

    [Fact]
    public void Owned_model_maps_universal_exercise_answer_question_type()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var entity = context.Model.FindEntityType(typeof(ExerciseSessionAnswer))!;
        var table = StoreObjectIdentifier.Table("exercise_session_answers", "speed_reading");

        entity.FindProperty(nameof(ExerciseSessionAnswer.QuestionType))!
            .GetColumnName(table).Should().Be("question_type");
        entity.GetIndexes().Should().Contain(index => index.Properties
            .Select(property => property.Name)
            .SequenceEqual(new[] { nameof(ExerciseSessionAnswer.SessionId), nameof(ExerciseSessionAnswer.QuestionType) }));
    }

    [Fact]
    public void Gamification_updates_use_an_optimistic_concurrency_token()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var entity = context.Model.FindEntityType(typeof(UserGamification))!;

        entity.FindProperty(nameof(UserGamification.UpdatedAt))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void Exercise_session_updates_use_an_optimistic_concurrency_token()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var entity = context.Model.FindEntityType(typeof(ExerciseSession))!;

        entity.FindProperty(nameof(ExerciseSession.UpdatedAt))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void Owned_context_discovers_reading_session_answer_migration()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Database.GetMigrations()
            .Should().ContainInOrder(
                "20260916100000_AddReadingSessionAnswers",
                "20260916101000_AddReadingQuestionSnapshots",
                "20260916102000_AddExerciseAnswerQuestionType",
                "20260916103000_AddReadingSessionMeasurementStatus",
                "20260916104000_AddReadingWordCountSnapshots");
    }
}
