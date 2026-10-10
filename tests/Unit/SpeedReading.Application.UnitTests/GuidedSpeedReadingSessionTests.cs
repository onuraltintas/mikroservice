using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class GuidedSpeedReadingSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_out_of_band_texts_in_automatic_and_explicit_selection(bool pinned)
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var text = ReadingText.Create(Guid.NewGuid(), "Kısa", "bir iki", difficultyLevel: 2);
        db.ReadingTexts.Add(text);
        await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(student,
            new() { ExerciseId = exercise, ReadingTextId = pinned ? text.Id : null }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Requires_verified_display_and_saves_tempo_not_reading_measurement()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Uygun", "bir iki üç dört", difficultyLevel: 2));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        Assert.Equal(1, started.InitialData.GetProperty("groupingChunkSize").GetInt32());
        Assert.Equal(160, started.InitialData.GetProperty("readingMinimumMs").GetInt32());
        Assert.Equal(0, started.InitialData.GetProperty("readingMaximumMs").GetInt32());
        await Assert.ThrowsAnyAsync<Exception>(() => service.CompleteAsync(student, started.SessionId, new()));
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
        Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-1);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM);
        Assert.Null(result.Accuracy);
        Assert.Null(result.ComprehensionScore);
        Assert.Equal(1500, result.DetailedResults.GetProperty("groupingDisplayPaceWpm").GetDecimal());
        Assert.Equal(100, result.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal());
        Assert.Equal(4, result.DetailedResults.GetProperty("wordCount").GetInt32());
        Assert.Equal(0, result.XpGained);
    }

    [Fact]
    public async Task Chooses_unused_matching_text_without_crossing_age_or_level()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var first = ReadingText.Create(Guid.NewGuid(), "Bir", "bir iki üç dört", difficultyLevel: 2);
        var second = ReadingText.Create(Guid.NewGuid(), "İki", "beş altı yedi sekiz", difficultyLevel: 2);
        db.ReadingTexts.AddRange(first, second,
            ReadingText.Create(Guid.NewGuid(), "Yanlış seviye", "bir iki üç dört", difficultyLevel: 1),
            ReadingText.Create(Guid.NewGuid(), "Yanlış yaş", "bir iki üç dört", difficultyLevel: 2, targetAgeGroupId: Guid.NewGuid()));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        var old = await db.ExerciseSessions.SingleAsync();
        old.Abandon(DateTime.UtcNow);
        await db.SaveChangesAsync();
        var next = await service.StartAsync(student, new() { ExerciseId = exercise });
        var selected = (await db.ExerciseSessions.SingleAsync(s => s.Id == next.SessionId)).ReadingTextId;
        Assert.NotEqual(old.ReadingTextId, selected);
        Assert.Contains(selected!.Value, new[] { first.Id, second.Id });
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise)> Seed(OwnedSpeedReadingDbContext db)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "SpeedReading", "Hızlı Okuma", "word_highlight");
        var exercise = Exercise.Create("Hızlı Okuma", "reading",
            """{"engineType":"word_highlight","engineConfig":{"mode":"speed_reading","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":3,"maxWordCount":5},"pacer":{"speedWpm":1500,"chunkSize":3},"timing":{"timeLimitSec":1}}}""",
            2, student, type.Id, targetAgeGroupId: Guid.NewGuid());
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise.Id);
    }
}
