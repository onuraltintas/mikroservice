using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class UnreadTextSelectionTests
{
    [Fact]
    public async Task Exercise_age_is_used_when_student_has_no_profile()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var childAge = Guid.NewGuid();
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
        var exercise = Exercise.Create("Çocuk", "Comprehension", "{}", 2, student, type.Id, targetAgeGroupId: childAge);
        var wrong = ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Yetişkin", "bir iki üç dört", difficultyLevel: 2, targetAgeGroupId: Guid.NewGuid());
        var correct = ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Çocuk", "beş altı yedi sekiz", difficultyLevel: 2, targetAgeGroupId: childAge);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        db.ReadingTexts.AddRange(wrong, correct);
        foreach (var text in new[] { wrong, correct })
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru?", "A", 0, 1, 1,
                optionA: "bir", optionB: "iki", optionC: "üç", optionD: "dört"));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        for (var index = 0; index < 8; index++)
        {
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
            Assert.Equal(correct.Id, session.ReadingTextId);
            session.Abandon(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("Comprehension", "reading_comprehension", true)]
    [InlineData("Chunking", "word_highlight", false)]
    [InlineData("ErrorAnalysis", "error_analysis", true)]
    public async Task Selection_respects_history_from_both_reading_flows(string typeName, string engine, bool exerciseHistory)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, engine);
        var configuration = engine == "error_analysis"
            ? """{"engineType":"error_analysis","engineConfig":{"content":{"source":"reading_text"},"errorCount":2}}""" : "{}";
        var exercise = Exercise.Create("Çocuk", typeName, configuration, 2, student, type.Id);
        var read = ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Okunmuş", "bir iki üç dört", difficultyLevel: 2);
        var unread = ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Yeni", "beş altı yedi sekiz", difficultyLevel: 2);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        db.ReadingTexts.AddRange(read, unread);
        foreach (var text in new[] { read, unread })
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru?", "A", 0, 1, 1,
                optionA: "bir", optionB: "iki", optionC: "üç", optionD: "dört"));
        if (exerciseHistory)
        {
            var prior = ExerciseSession.Start(student, exercise.Id, read.Id, 1, DateTime.UtcNow.AddMinutes(-5), null);
            prior.Abandon(DateTime.UtcNow.AddMinutes(-4));
            db.ExerciseSessions.Add(prior);
        }
        else
            db.ReadingSessions.Add(ReadingSession.Import(Guid.NewGuid(), student, read.Id, 60, 100, 1, 1,
                100, 100, DateTime.UtcNow, DateTime.UtcNow, null, null, null));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(unread.Id, (await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId)).ReadingTextId);
    }
}
