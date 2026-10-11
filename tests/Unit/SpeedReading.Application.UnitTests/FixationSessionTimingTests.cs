using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Assessment;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class FixationSessionTimingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_and_pinned_assessment_sessions_resume_with_their_original_protocol(bool assessment)
    {
        await using var db = CreateDb();
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
        const string configuration = """{"engineType":"motion_path","engineConfig":{"mode":"fixation","content":{"points":2,"peripheralCount":2},"timing":{"holdMs":100}}}""";
        var exercise = Exercise.Create("Sabitleme", "attention", configuration, 1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        var request = new StartExerciseSessionRequest { ExerciseId = exercise.Id };
        if (assessment) {
            var attemptId = Guid.NewGuid();
            request = new() { ExerciseId = exercise.Id, AssessmentAttemptId = attemptId };
            db.AssessmentAttempts.Add(AssessmentAttempt.Start(attemptId, student,
                AssessmentAttemptPhase.Baseline, "baseline-v1", "tr", null, 1, DateTime.UtcNow, student.ToString()));
            var snapshot = System.Text.Json.JsonSerializer.Serialize(new {
                version = 1, exercise = new { id = exercise.Id, title = exercise.Title, description = "attention",
                    typeName = "Fixation", difficultyLevel = 1, engineType = "motion_path", configurationJson = configuration },
                questions = Array.Empty<object>()
            });
            db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), attemptId,
                exercise.Id, null, "attention", 1, snapshot, DateTime.UtcNow, student.ToString()));
        }
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, request);
        Assert.Equal(assessment ? 1 : 2, started.InitialData.GetProperty("fixationProtocolVersion").GetInt32());
        var session = await db.ExerciseSessions.SingleAsync();
        if (!assessment) {
            var legacy = JsonNode.Parse(session.SessionDataJson)!; legacy["fixationProtocolVersion"] = 1;
            session.SetState(legacy.ToJsonString()); await db.SaveChangesAsync();
        }
        var presented = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_present" });
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["fixationPresentedAt"] = DateTime.UtcNow.AddSeconds(-1);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var answer = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_answer", Answers = Stimuli(presented) });
        Assert.True(answer.IsValid);
        if (assessment) Assert.Null(answer.IsCorrect); else Assert.True(answer.IsCorrect);
        Assert.True(JsonNode.Parse(session.SessionDataJson)!["fixationRoundResults"]![0]!["isCorrect"]!.GetValue<bool>());
        await service.PauseAsync(student, started.SessionId); db.ChangeTracker.Clear();
        await service.ResumeAsync(student, started.SessionId);
        var resumed = await service.StartAsync(student, request);
        Assert.Equal(started.SessionId, resumed.SessionId);
        Assert.Equal(1, resumed.InitialData.GetProperty("fixationProtocolVersion").GetInt32());
        Assert.Equal(1, resumed.InitialData.GetProperty("fixationRound").GetInt32());
        Assert.Equal(2, resumed.TotalSteps);
    }

    [Fact]
    public async Task New_fixation_protocol_preserves_the_previous_stimuli_across_pause_without_exposing_them()
    {
        await using var db = CreateDb();
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
        var exercise = Exercise.Create("Sabitleme", "attention",
            """{"engineType":"motion_path","engineConfig":{"mode":"fixation","content":{"points":6,"peripheralCount":4,"maxPeripheralCount":4},"timing":{"holdMs":100}}}""",
            5, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(2, started.InitialData.GetProperty("fixationProtocolVersion").GetInt32());
        List<string>? previous = null;
        for (var round = 0; round < 6; round++)
        {
            if (round == 2) {
                await service.PauseAsync(student, started.SessionId);
                db.ChangeTracker.Clear(); await service.ResumeAsync(student, started.SessionId);
            }
            var presentation = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "fixation_present" });
            var stimuli = Stimuli(presentation);
            Assert.Equal(4, stimuli.Count);
            Assert.Equal(4, stimuli.Distinct().Count());
            if (previous is not null) Assert.Empty(stimuli.Intersect(previous));
            previous = stimuli;
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["fixationPresentedAt"] = DateTime.UtcNow.AddSeconds(-1);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId,
                new() { Action = "fixation_answer", Answers = stimuli.AsEnumerable().Reverse().ToList() })).IsCorrect);
        }
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Equal(100, result.Accuracy);
        Assert.Equal(6, result.DetailedResults.GetProperty("fixationRoundResults").GetArrayLength());
        Assert.False(result.DetailedResults.TryGetProperty("fixationPreviousStimuli", out _));
    }

    [Fact]
    public async Task Restarts_legacy_fixation_without_mixing_old_round_history()
    {
        await using var db = CreateDb();
        var (service, student, started) = await Start(db);
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state.AsObject().Remove("fixationProtocolVersion");
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var restarted = await service.StartAsync(student, new() { ExerciseId = started.ExerciseId });
        Assert.NotEqual(started.SessionId, restarted.SessionId);
        Assert.Equal(2, restarted.InitialData.GetProperty("fixationProtocolVersion").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_history_is_not_published_as_an_empty_measurement(bool assessment)
    {
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var state = owned.GetMethod("DeserializeState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            [$$"""{"engineType":"motion_path","fixationPeripheralCount":2,"isAssessmentMode":{{assessment.ToString().ToLowerInvariant()}}}"""])!;
        var json = (System.Text.Json.JsonElement)owned.GetMethod("ToPublicJson", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state])!;
        Assert.False(json.TryGetProperty("fixationRoundResults", out _));
    }
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
