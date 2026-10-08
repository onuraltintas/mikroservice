using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class RegressionTextSelectionTests
{
    [Fact]
    public async Task PrefersUnusedMatchingTextAfterAnAttempt()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid(); var exercise = Guid.NewGuid(); var type = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "Regresyon", "Regresyon", "regression_reduction"));
        db.Exercises.Add(Exercise.Create("Regresyon", "reading", "{}", 3, student, type, id: exercise));
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        db.ReadingTexts.Add(ReadingText.Create(first, "First", "bir iki üç", difficultyLevel: 3));
        db.ReadingTexts.Add(ReadingText.Create(second, "Second", "dört beş altı", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        await service.StartAsync(student, new StartExerciseSessionRequest { ExerciseId = exercise });
        var old = await db.ExerciseSessions.SingleAsync();
        Assert.Contains(old.ReadingTextId!.Value, new[] { first, second });
        var remaining = old.ReadingTextId == first ? second : first;
        old.Abandon(DateTime.UtcNow); await db.SaveChangesAsync();
        var next = await service.StartAsync(student, new StartExerciseSessionRequest { ExerciseId = exercise });
        Assert.Equal(remaining, (await db.ExerciseSessions.SingleAsync(session => session.Id == next.SessionId)).ReadingTextId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoesNotSilentlyUseAnotherLevelWhenMatchingTextIsMissing(bool explicitlySelected)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid(); var exercise = Guid.NewGuid(); var type = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "Regresyon", "Regresyon", "regression_reduction"));
        db.Exercises.Add(Exercise.Create("Regresyon", "reading", "{}", 3, student, type, id: exercise));
        var wrongText = Guid.NewGuid();
        db.ReadingTexts.Add(ReadingText.Create(wrongText, "Wrong", "bir iki üç", difficultyLevel: 1));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var request = new StartExerciseSessionRequest { ExerciseId = exercise,
            ReadingTextId = explicitlySelected ? wrongText : null };
        if (explicitlySelected)
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.StartAsync(student, request));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(student, request));
    }
}
