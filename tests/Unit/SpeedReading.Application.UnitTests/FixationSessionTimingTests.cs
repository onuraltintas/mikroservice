using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class FixationSessionTimingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_answers_before_exposure_including_paused_time(bool pause)
    {
        await using var db = CreateDb();
        var (service, student, started) = await Start(db);
        var present = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_present" });
        if (pause)
        {
            await service.PauseAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["fixationPausedAt"] = DateTime.UtcNow.AddSeconds(-65);
            state["fixationPresentedAt"] = DateTime.UtcNow.AddMilliseconds(-65_100);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            await service.ResumeAsync(student, started.SessionId);
        }
        var answer = await service.ValidateActionAsync(student, started.SessionId,
            new() { Action = "fixation_answer", Answers = Stimuli(present) });
        Assert.False(answer.IsValid);
        Assert.Equal(0, (await db.ExerciseSessions.SingleAsync()).CurrentStep);
    }

    [Fact]
    public async Task Saves_verified_history_and_response_latency_without_wpm()
    {
        await using var db = CreateDb();
        var (service, student, started) = await Start(db);
        var present = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_present" });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["fixationPresentedAt"] = DateTime.UtcNow.AddMilliseconds(-1550);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var answer = await service.ValidateActionAsync(student, started.SessionId,
            new() { Action = "fixation_answer", Answers = Stimuli(present) });
        Assert.True(answer.IsValid);
        Assert.InRange(answer.FeedbackData!.Value.GetProperty("responseTimeMs").GetInt32(), 190, 1000);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        Assert.Equal(1, result.DetailedResults.GetProperty("fixationRoundResults").GetArrayLength());
        Assert.Equal(100, result.Accuracy);
    }

    private static List<string> Stimuli(ExerciseActionValidationResponse response) =>
        response.FeedbackData!.Value.GetProperty("stimuli").EnumerateArray().Select(item => item.GetString()!).ToList();

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions, Guid, StartExerciseSessionResponse)> Start(OwnedSpeedReadingDbContext db)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
        var exercise = Exercise.Create("Sabitleme", "attention",
            """{"engineType":"motion_path","mode":"fixation","content":{"points":1,"peripheralCount":2},"timing":{"holdMs":1000}}""", 1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (service, student, await service.StartAsync(student, new() { ExerciseId = exercise.Id }));
    }
}
