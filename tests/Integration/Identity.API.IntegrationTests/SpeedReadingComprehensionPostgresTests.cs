using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Programs;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingComprehensionPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("learning")]
    [InlineData("evaluation")]
    public async Task Configured_mode_selects_sufficient_questions_and_keeps_learning_out_of_reading_measurements(string mode)
    {
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
            var exercise = Exercise.Create("Anlama", "reading",
                JsonSerializer.Serialize(new { engineType = "reading_comprehension", engineConfig = new {
                    comprehensionMode = mode, content = new { minWordCount = 80, maxWordCount = 120, questionCount = 2 } } }),
                2, user, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            var candidates = new[] {
                ReadingText.Create(Guid.NewGuid(), "Kısa", "bir iki", difficultyLevel: 2),
                ReadingText.Create(Guid.NewGuid(), "Az soru", string.Join(" ", Enumerable.Repeat("bahçe", 100)), difficultyLevel: 2),
                ReadingText.Create(Guid.NewGuid(), "Uygun", string.Join(" ", Enumerable.Repeat("bahçe", 100)), difficultyLevel: 2),
                ReadingText.Create(Guid.NewGuid(), "Yanlış seviye", string.Join(" ", Enumerable.Repeat("bahçe", 100)), difficultyLevel: 1) };
            db.ReadingTexts.AddRange(candidates);
            for (var index = 0; index < candidates.Length; index++)
                for (var order = 0; order < (index == 1 ? 1 : 3); order++)
                    db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), candidates[index].Id, $"Soru {order}", "A", order,
                        1, 1, 2, "Metnin konusu bahçedir.", "Bahçe", "Deniz", "Dağ", "Köprü"));
            await db.SaveChangesAsync();
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
            Assert.Equal(2, started.InitialData.GetProperty("questions").GetArrayLength());
            var session = await db.ExerciseSessions.SingleAsync();
            Assert.Equal(candidates[2].Id, session.ReadingTextId);
            var first = started.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid();
            Assert.False((await service.ValidateActionAsync(user, session.Id, new() { Action = "answer_question", QuestionId = first, Answer = "A" })).IsValid);
            await service.ValidateActionAsync(user, session.Id, new() { Action = "start_reading" });
            var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-30);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" });
            foreach (var question in started.InitialData.GetProperty("questions").EnumerateArray())
            {
                var answer = await service.ValidateActionAsync(user, session.Id,
                    new() { Action = "answer_question", QuestionId = question.GetProperty("questionId").GetGuid(), Answer = "A" });
                Assert.Equal(mode == "learning" ? true : null, answer.IsCorrect);
            }
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), session.Id, new()));
            var result = await service.CompleteAsync(user, session.Id, new());
            Assert.Equal(100m, result.Score);
            Assert.Equal(mode, result.DetailedResults.GetProperty("comprehensionMode").GetString());
            Assert.Equal(2, result.DetailedResults.GetProperty("answers").GetArrayLength());
            Assert.Equal(mode == "learning" ? 0 : 1, await db.ReadingSessions.CountAsync());
            Assert.Equal(mode == "learning", result.RawWPM is null);
            var adaptiveType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveLearning")!;
            var adaptive = (SpeedReading.Application.AdaptiveLearning.ISpeedReadingAdaptiveLearning)Activator.CreateInstance(adaptiveType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            Assert.Equal(mode == "learning" ? 0m : 100m, (await adaptive.GetProfileAsync(user)).AverageComprehension);
            await service.CompleteAsync(user, session.Id, new());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Incomplete_reading_is_preserved_but_cannot_advance_daily_program()
    {
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var type = ExerciseType.Create(Guid.NewGuid(), "Anlama", "Anlama", "reading_comprehension");
            var exercise = Exercise.Create("Anlama", "reading", "{}", 1, user, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100,
                "{}", 1, 2, 5, 1, 2, true, 1, 0, null, false, now, null, null, null);
            db.ProgramTemplates.Add(template);
            var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now, true);
            progress.SetSchedule(JsonSerializer.Serialize(new[] { new { weekNumber = 1, dayNumber = 1, order = 1, exerciseId = exercise.Id } }), user, now);
            db.StudentProgramProgresses.Add(progress);
            var session = ExerciseSession.Start(user, exercise.Id, null, 1, now, null);
            session.SetState("""{"readingIncomplete":true}""");
            session.Complete(now.AddSeconds(5));
            db.ExerciseSessions.Add(session);
            db.ExerciseSessionResults.Add(SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user,
                exercise.Id, null, 0, 5, 0, 0, 0, 0, now, isMeasured: false));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var typeInfo = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!;
            var daily = (ISpeedReadingDailyProgress)Activator.CreateInstance(typeInfo, db, null)!;
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => daily.CompleteExerciseAsync(user,
                new CompleteDailyExerciseRequest { ExerciseId = exercise.Id, SessionId = session.Id }, session.Id.ToString()));
            Assert.Equal("DailyProgress.IncompleteReading", error.Code);
            Assert.Equal(0, (await db.StudentProgramProgresses.SingleAsync()).DaysCompleted);
            Assert.Empty(await db.DailyExerciseLogs.ToListAsync());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

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
