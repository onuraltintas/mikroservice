using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class GroupingSessionTests
{
    [Fact]
    public async Task Rejects_explicit_wrong_level_text()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var text = ReadingText.Create(Guid.NewGuid(), "Yanlış", "bir iki", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise, ReadingTextId = text.Id });
        await start.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Root_legacy_timing_matches_the_frontend_display_cycle()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db,
            """{"engineType":"word_highlight","timing":{"durationMs":850,"delayMs":450},"engineConfig":{"mode":"chunking","content":{"chunkSize":2}}}""");
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Metin", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        started.InitialData.GetProperty("readingMinimumMs").GetInt32().Should().Be(2600);
    }

    [Fact]
    public async Task Restarts_legacy_active_grouping_without_verifiable_timing()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Metin", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var old = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
        state.AsObject().Remove("groupingDisplayPaceWpm");
        state.AsObject().Remove("readingMinimumMs");
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        var restarted = await service.StartAsync(student, new() { ExerciseId = exercise });
        restarted.SessionId.Should().NotBe(old.SessionId);
    }
    [Fact]
    public async Task Requires_reading_tracking_and_keeps_tempo_separate_from_measurement()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Metin", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        var early = () => service.CompleteAsync(student, started.SessionId, new());
        await early.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid.Should().BeTrue();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        var session = await db.ExerciseSessions.SingleAsync();
        var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-2);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.RawWPM.Should().BeNull();
        result.Accuracy.Should().BeNull();
        result.XpGained.Should().Be(0);
        result.DetailedResults.GetProperty("groupingDisplayPaceWpm").GetDecimal().Should().Be(200);
        result.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal().Should().Be(100);
        (await service.CompleteAsync(student, started.SessionId, new())).RawWPM.Should().BeNull();
        db.ExerciseSessionResults.Should().ContainSingle();
    }
    [Fact]
    public async Task Does_not_fall_back_to_a_different_text_level()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Başka seviye", "bir iki üç dört", difficultyLevel: 1));
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise });
        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rotates_matching_texts_using_student_session_history()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var firstId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        db.ReadingTexts.Add(ReadingText.Create(firstId, "Bir", "bir iki üç dört", difficultyLevel: 3));
        db.ReadingTexts.Add(ReadingText.Create(secondId, "İki", "beş altı yedi sekiz", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var first = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var selected = session.ReadingTextId;
        session.Abandon(DateTime.UtcNow);
        await db.SaveChangesAsync();
        await service.StartAsync(student, new() { ExerciseId = exercise });
        (await db.ExerciseSessions.SingleAsync(item => item.Id != session.Id)).ReadingTextId.Should().NotBe(selected!.Value);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise)> Seed(OwnedSpeedReadingDbContext db, string? config = null)
    {
        var student = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "Chunking", "Gruplama", "word_highlight"));
        db.Exercises.Add(Exercise.Create("Gruplama", "reading",
            config ?? """{"engineType":"word_highlight","engineConfig":{"mode":"chunking","pacer":{"chunkSize":2,"speedWpm":200}}}""",
            3, student, type, id: exercise));
        await db.SaveChangesAsync();
        var ownedType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(ownedType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (service, student, exercise);
    }
}
