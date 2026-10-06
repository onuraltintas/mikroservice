using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingRsvpPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Persists_verified_presentation_and_optional_comprehension(bool partial, bool evaluation)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database supplied by PostgresFixture is used.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
            var config = new JsonObject { ["engineConfig"] = new JsonObject {
                ["mode"] = "rsvp", ["readingPurpose"] = evaluation ? "evaluation" : "practice",
                ["timing"] = new JsonObject { ["durationMs"] = 200, ["intervalMs"] = 100 },
                ["visuals"] = new JsonObject { ["showFixation"] = false }
            }};
            var exercise = Exercise.Create("RSVP", "reading", config.ToJsonString(), 3, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            var questionIds = new Dictionary<Guid, Guid>();
            for (var index = 0; index < 2; index++)
            {
                var text = ReadingText.Create(Guid.NewGuid(), "Metin", "bir iki üç dört", difficultyLevel: 3);
                db.ReadingTexts.Add(text);
                var question = Guid.NewGuid(); questionIds[text.Id] = question;
                db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Soru", "A", 0, 1, 1,
                    optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            }
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1));
            await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(1100, started.InitialData.GetProperty("readingMinimumMs").GetInt32());
            Assert.Equal(evaluation ? 1 : 0, started.InitialData.GetProperty("questions").GetArrayLength());
            if (evaluation) Assert.Throws<KeyNotFoundException>(() => started.InitialData.GetProperty("questions")[0].GetProperty("correctAnswer"));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync();
            var textId = session.ReadingTextId!.Value;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -600 : -1200);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading", IsTimeout = partial })).IsValid);
            if (evaluation) Assert.True((await service.ValidateActionAsync(student, started.SessionId,
                new() { Action = "answer_question", QuestionId = questionIds[textId], Answer = "A" })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            Assert.Equal(evaluation ? (decimal?)100 : null, result.ComprehensionScore);
            Assert.Equal(partial ? 2 : 4, result.DetailedResults.GetProperty("rsvpPresentedWords").GetInt32());
            Assert.Equal(partial ? 50 : 100, result.DetailedResults.GetProperty("rsvpCompletionPercent").GetDecimal());
            Assert.Equal(partial, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            if (!evaluation) Assert.Equal(0, result.XpGained);
            db.ChangeTracker.Clear();
            Assert.Null((await service.CompleteAsync(student, started.SessionId, new())).RawWPM);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            var next = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id == next.SessionId)).ReadingTextId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
