using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SpeedReading.Application.Analytics;
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
                .SequenceEqual([nameof(ReadingSessionAnswer.SessionId), nameof(ReadingSessionAnswer.QuestionId)]));
    }

    [Fact]
    public void Owned_context_discovers_reading_session_answer_migration()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Database.GetMigrations()
            .Should().Contain("20260916100000_AddReadingSessionAnswers");
    }
}
