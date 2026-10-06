using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class FreeReadingPersistenceTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(201)]
    public async Task Explicit_text_cannot_bypass_length_limits(int words)
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await Seed(db, user);
        var text = Text(Guid.NewGuid(), exercise, 2, words); db.ReadingTexts.Add(text); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise, ReadingTextId = text.Id }));
        Assert.Empty(db.ExerciseSessions);
    }
    [Fact]
    public async Task Selection_respects_level_length_and_rotates_read_texts()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await Seed(db, user);
        var shortId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var firstId = Guid.Parse("10000000-0000-4000-8000-000000000002");
        var secondId = Guid.Parse("10000000-0000-4000-8000-000000000003");
        db.ReadingTexts.AddRange(Text(shortId, exercise, 2, 2), Text(firstId, exercise, 2, 100), Text(secondId, exercise, 2, 100));
        await db.SaveChangesAsync(); var service = Service(db);
        var first = await service.StartAsync(user, new() { ExerciseId = exercise });
        Assert.Equal(firstId, (await db.ExerciseSessions.SingleAsync()).ReadingTextId);
        var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-30); session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        await service.ValidateActionAsync(user, first.SessionId, new() { Action = "finish_reading" });
        await service.CompleteAsync(user, first.SessionId, new());
        var second = await service.StartAsync(user, new() { ExerciseId = exercise });
        Assert.Equal(secondId, (await db.ExerciseSessions.SingleAsync(s => s.Id == second.SessionId)).ReadingTextId);
    }
    [Fact]
    public async Task No_matching_level_does_not_fall_back_to_other_level()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await Seed(db, user);
        db.ReadingTexts.Add(Text(Guid.NewGuid(), exercise, 1, 100)); await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise }));
        Assert.Empty(db.ExerciseSessions);
    }
    [Fact]
    public async Task Server_enforces_minimum_and_uses_precise_active_reading_time()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await Seed(db, user);
        db.ReadingTexts.Add(Text(Guid.NewGuid(), exercise, 2, 100)); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
        Assert.False((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" })).IsValid);
        var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
        var end = DateTime.UtcNow.AddSeconds(-2);
        state["readingStartTime"] = end.AddMilliseconds(-30500); state["readingEndTime"] = end;
        state["readingPausedMilliseconds"] = 250; state["readingPausedSeconds"] = 0;
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(198.35m, result.RawWPM);
        Assert.Null(result.ComprehensionScore);
    }
    private static ReadingText Text(Guid id, Guid exercise, int level, int count) => ReadingText.Create(id, "Metin", string.Join(" ", Enumerable.Repeat("kelime", count)), difficultyLevel: level, exerciseId: exercise);
    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    private static async Task<Guid> Seed(OwnedSpeedReadingDbContext db, Guid user)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "FreeReading", "Serbest Okuma", "free_reading");
        var exercise = Exercise.Create("Serbest Okuma", "free_reading", "{\"engineType\":\"free_reading\",\"engineConfig\":{\"content\":{\"minWordCount\":50,\"maxWordCount\":200},\"timing\":{\"minReadingTimeMs\":3000,\"maxReadingTimeMs\":60000}}}", 2, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return exercise.Id;
    }
}
