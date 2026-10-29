using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingFixationPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Saves_owned_fixation_rounds_with_safe_letter_limit_and_idempotent_completion(int maximumLetters)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database is used here.
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
            var config = JsonSerializer.Serialize(new { engineType = "motion_path", engineConfig = new {
                mode = "fixation", content = new { points = 2, peripheralCount = 99, maxPeripheralCount = maximumLetters },
                timing = new { holdMs = 100 }
            } });
            var exercise = Exercise.Create("Sabitleme", "attention", config, 1, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(maximumLetters, started.InitialData.GetProperty("fixationPeripheralCount").GetInt32());
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "fixation_present" }));
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
            List<string>? previous = null;
            for (var round = 0; round < 2; round++) {
                var present = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_present" });
                var stimuli = present.FeedbackData!.Value.GetProperty("stimuli").EnumerateArray().Select(item => item.GetString()!).ToList();
                Assert.Equal(maximumLetters, stimuli.Count);
                if (previous is not null) Assert.Empty(stimuli.Intersect(previous));
                previous = stimuli;
                var session = await db.ExerciseSessions.SingleAsync();
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["fixationPresentedAt"] = DateTime.UtcNow.AddSeconds(-1);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                var answer = await service.ValidateActionAsync(student, started.SessionId, new() {
                    Action = "fixation_answer", Answers = round == 0 ? stimuli : ["?"]
                });
                Assert.True(answer.IsValid); Assert.Equal(round == 0, answer.IsCorrect);
                Assert.False((await service.ValidateActionAsync(student, started.SessionId,
                    new() { Action = "fixation_answer", Answers = stimuli })).IsValid);
            }
            var result = await service.CompleteAsync(student, started.SessionId,
                new() { CustomData = new() { ["accuracy"] = JsonSerializer.SerializeToElement(100) } });
            Assert.Equal(50, result.Accuracy); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            Assert.Equal(2, result.DetailedResults.GetProperty("fixationRoundResults").GetArrayLength());
            Assert.False(result.DetailedResults.TryGetProperty("fixationPreviousStimuli", out _));
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(50, repeated.Accuracy);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
