using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

const string database = "visual_expansion_step_drill_20261014";
var connection = Environment.GetEnvironmentVariable("VISUAL_EXPANSION_DRILL_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection");
if (new NpgsqlConnectionStringBuilder(connection).Database != database) throw new InvalidOperationException("Only the named disposable database is allowed");
var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options;
var checks = new List<string>();
void Check(bool success, string name) { if (!success) throw new InvalidOperationException(name); checks.Add(name); }
foreach (var mode in new[] { "training", "legacy-training", "legacy-assessment", "block-assessment" }) {
    await using var db = new OwnedSpeedReadingDbContext(options);
    var student = Guid.NewGuid();
    var type = ExerciseType.Create(Guid.NewGuid(), "VisualExpansionDrill" + Guid.NewGuid().ToString("N"), "Release fixture", "visual_expansion");
    var configuration = JsonSerializer.Serialize(new { engineType = "visual_expansion", engineConfig = new {
        rounds = mode == "block-assessment" ? 21 : 6, startDegrees = 8, targetDegrees = 16, displayDurationMs = 400,
        assessmentProtocol = mode == "block-assessment" ? new { version = 2, practiceRounds = 3, roundsPerBlock = 6, blockValues = new[] { 6, 10, 14 } } : null
    } }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    var exercise = Exercise.Create("Visual expansion release fixture", "attention", configuration, 1, student, type.Id);
    db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
    var request = new StartExerciseSessionRequest { ExerciseId = exercise.Id };
    var training = mode is "training" or "legacy-training";
    if (!training) {
        var attempt = Guid.NewGuid(); request = new() { ExerciseId = exercise.Id, AssessmentAttemptId = attempt };
        db.AssessmentAttempts.Add(AssessmentAttempt.Start(attempt, student, AssessmentAttemptPhase.Baseline, "baseline-v1", "tr", null, 1, DateTime.UtcNow, student.ToString()));
        var snapshot = JsonSerializer.Serialize(new { version = 1, exercise = new {
            id = exercise.Id, title = exercise.Title, description = "attention", typeName = "VisualExpansion",
            difficultyLevel = 1, engineType = "visual_expansion", configurationJson = configuration
        }, questions = Array.Empty<object>() });
        db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), attempt, exercise.Id, null, "attention", 1, snapshot, DateTime.UtcNow, student.ToString()));
    }
    await db.SaveChangesAsync();
    var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    var started = await service.StartAsync(student, request);
    if (mode == "legacy-training") {
        var legacy = await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        var legacyState = JsonNode.Parse(legacy.SessionDataJson)!;
        legacyState["visualExpansionProtocolVersion"] = 1;
        legacy.SetState(legacyState.ToJsonString()); await db.SaveChangesAsync();
    }
    var rounds = mode == "block-assessment" ? 21 : 6;
    for (var round = 0; round < rounds; round++) {
        if (round == 2) { await service.PauseAsync(student, started.SessionId); db.ChangeTracker.Clear(); await service.ResumeAsync(student, started.SessionId); }
        var response = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "visual_expansion_present" });
        var data = response.FeedbackData!.Value;
        var distance = training ? new[] { 8, 9, 9, 10, 11, 12 }[round]
            : mode == "legacy-assessment" ? Math.Min(16, 8 + 2 * round)
            : round < 9 ? 6 : round < 15 ? 10 : 14;
        Check(data.GetProperty("degrees").GetInt32() == distance, mode + "-distance-" + round);
        var duration = mode == "legacy-assessment" ? new[] { 400, 380, 361, 343, 326, 310 }[round] : training ? 400 : 200;
        Check(data.GetProperty("displayDurationMs").GetInt32() == duration, mode + "-duration-" + round);
        var answers = data.GetProperty("stimuli").EnumerateArray().Select(item => item.GetString()!).ToList();
        var session = await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["visualExpansionPresentedAt"] = DateTime.UtcNow.AddSeconds(-1);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var answer = await service.ValidateActionAsync(student, started.SessionId, new() {
            Action = "visual_expansion_answer", Answers = training && round == 1 ? ["?", "?"] : answers
        });
        Check(answer.IsValid, mode + "-accepted-" + round);
    }
    var result = await service.CompleteAsync(student, started.SessionId, new() { IsAssessmentMode = !training });
    Check(result.RawWPM is null, mode + "-no-reading-wpm");
    if (mode == "training") Check(result.DetailedResults.GetProperty("visualExpansionMaxPresentedDistance").GetInt32() == 12, "training-presented-distance-only");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = checks.Count, checks }));
