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

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise)> Seed(OwnedSpeedReadingDbContext db)
    {
        var student = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "Chunking", "Gruplama", "word_highlight"));
        db.Exercises.Add(Exercise.Create("Gruplama", "reading",
            """{"engineType":"word_highlight","engineConfig":{"mode":"chunking","pacer":{"chunkSize":2,"speedWpm":200}}}""",
            3, student, type, id: exercise));
        await db.SaveChangesAsync();
        var ownedType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(ownedType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (service, student, exercise);
    }
}
