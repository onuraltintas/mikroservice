using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class TextFadeSessionTests
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
    public async Task Requires_matching_active_text_instead_of_another_level_or_sample()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "bir iki", difficultyLevel: 1));
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise });
        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rotates_matching_text_using_only_this_students_history()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000001"), "A", "bir iki üç dört", difficultyLevel: 3));
        db.ReadingTexts.Add(ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), "B", "beş altı yedi sekiz", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var first = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var selected = session.ReadingTextId;
        session.Abandon(DateTime.UtcNow);
        await db.SaveChangesAsync();
        await service.StartAsync(student, new() { ExerciseId = exercise });
        (await db.ExerciseSessions.SingleAsync(item => item.Id != first.SessionId)).ReadingTextId.Should().NotBe(selected!.Value);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise)> Seed(OwnedSpeedReadingDbContext db, string? config = null)
    {
        var student = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "TextFading", "Metin Solma", "text_fade"));
        db.Exercises.Add(Exercise.Create("Metin Solma", "reading",
            config ?? """{"engineType":"text_fade","engineConfig":{"fading":{"speedWpm":200,"lagMs":1000}}}""",
            3, student, type, id: exercise));
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise);
    }
}
