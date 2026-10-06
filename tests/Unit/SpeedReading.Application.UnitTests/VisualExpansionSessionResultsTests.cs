using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualExpansionSessionResultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_public_state_does_not_publish_unmeasured_visual_metrics(bool assessment)
    {
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var deserialize = owned.GetMethod("DeserializeState", BindingFlags.Static | BindingFlags.NonPublic)!;
        var state = deserialize.Invoke(null, [$$"""{"engineType":"visual_expansion","exerciseTypeName":"VisualExpansion","isAssessmentMode":{{assessment.ToString().ToLowerInvariant()}}}"""])!;
        var json = (System.Text.Json.JsonElement)owned.GetMethod("ToPublicJson", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state])!;
        Assert.False(json.TryGetProperty("visualExpansionRoundResults", out _));
        Assert.False(json.TryGetProperty("visualExpansionMaxPresentedDistance", out _));
        Assert.False(json.TryGetProperty("visualExpansionAverageResponseTimeMs", out _));
    }
    [Fact]
    public async Task Restarts_legacy_attempt_instead_of_publishing_an_incomplete_verified_history()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        var exercise = Exercise.Create("Görsel Genişleme", "attention", """{"engineType":"visual_expansion","rounds":2}""", 1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!.AsObject();
        state.Remove("visualExpansionProtocolVersion"); state["visualExpansionRound"] = 2;
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
        var restarted = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.NotEqual(started.SessionId, restarted.SessionId);
        Assert.Equal(0, restarted.InitialData.GetProperty("visualExpansionRound").GetInt32());
    }
    [Fact]
    public async Task Stores_verified_rounds_without_measured_reading_speed_or_client_forged_metrics()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        var exercise = Exercise.Create("Görsel Genişleme", "attention",
            """{"engineType":"visual_expansion","rounds":2,"startDegrees":10,"targetDegrees":20,"timing":{"durationMs":100}}""",
            3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "visual_expansion_present" }));
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
        for (var round = 0; round < 2; round++)
        {
            var present = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "visual_expansion_present" });
            var expected = present.FeedbackData!.Value.GetProperty("stimuli").EnumerateArray().Select(item => item.GetString()!).ToList();
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            if (round == 0)
            {
                await service.PauseAsync(student, started.SessionId);
                state = JsonNode.Parse(session.SessionDataJson)!;
                state["visualExpansionPausedAt"] = DateTime.UtcNow.AddMilliseconds(-2000);
                state["visualExpansionPresentedAt"] = DateTime.UtcNow.AddMilliseconds(-2600);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                await service.ResumeAsync(student, started.SessionId);
                state = JsonNode.Parse(session.SessionDataJson)!;
            }
            else state["visualExpansionPresentedAt"] = DateTime.UtcNow.AddMilliseconds(-600);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var answer = await service.ValidateActionAsync(student, started.SessionId,
                new() { Action = "visual_expansion_answer", Answers = round == 0 ? expected : ["?", "?"] });
            Assert.True(answer.IsValid);
            Assert.InRange(answer.FeedbackData!.Value.GetProperty("responseTimeMs").GetInt32(), 490, 1000);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "visual_expansion_answer", Answers = expected })).IsValid);
        }
        var result = await service.CompleteAsync(student, started.SessionId, new() { CustomData = new() { ["maxDegreesReached"] = System.Text.Json.JsonSerializer.SerializeToElement(999) } });
        Assert.Equal(50, result.Accuracy);
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        Assert.Equal(12, result.DetailedResults.GetProperty("visualExpansionMaxPresentedDistance").GetInt32());
        Assert.Equal(2, result.DetailedResults.GetProperty("visualExpansionRoundResults").GetArrayLength());
        Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
    }
}
