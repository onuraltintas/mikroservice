using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingComprehensionPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Comprehension_selection_translates_and_uses_unread_matching_level()
    {
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database is reset here.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var exercise = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            var read = Guid.NewGuid();
            var unread = Guid.NewGuid();
            var wrongLevel = Guid.NewGuid();
            db.ExerciseTypes.Add(ExerciseType.Create(typeId, "Anlama", "Anlama", "reading_comprehension"));
            db.Exercises.Add(Exercise.Create("Anlama", "reading", "{}", 3, user, typeId, id: exercise));
            db.ReadingTexts.AddRange(
                ReadingText.Create(read, "Read", "Daha önce okunan metin.", exerciseId: exercise, difficultyLevel: 3),
                ReadingText.Create(unread, "Unread", "Yeni uygun okuma metni.", difficultyLevel: 3),
                ReadingText.Create(wrongLevel, "Wrong level", "Başka seviyedeki okuma metni.", exerciseId: exercise, difficultyLevel: 1));
            foreach (var id in new[] { read, unread, wrongLevel })
                db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), id, "Soru", "A", 0, 1, 1,
                    optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            db.ReadingSessions.Add(ReadingSession.Import(Guid.NewGuid(), user, read, 60, 100, 1, 1,
                100, 100, DateTime.UtcNow, DateTime.UtcNow, null, null, null));
            await db.SaveChangesAsync();
            var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(user, new StartExerciseSessionRequest { ExerciseId = exercise });
            Assert.Equal(unread, (await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId)).ReadingTextId);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
