using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingVisualExpansionPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("horizontal", "letter")]
    [InlineData("vertical", "symbol")]
    [InlineData("radial", "word")]
    public async Task Stores_verified_rounds_without_measured_reading_speed_or_client_forged_metrics(string pattern, string stimulusType)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Fixture owns a disposable Testcontainers database, never production.
        await db.Database.EnsureDeletedAsync();
        try {
        await db.Database.EnsureCreatedAsync();
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        var exercise = Exercise.Create("Görsel Genişleme", "attention",
            System.Text.Json.JsonSerializer.Serialize(new { engineType = "visual_expansion", rounds = 4, startDegrees = 10, targetDegrees = 20, timing = new { durationMs = 100 }, expansion = new { pattern, stimulusType } }),
            3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "visual_expansion_present" }));
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
        for (var round = 0; round < 4; round++)
        {
            var present = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "visual_expansion_present" });
            Assert.Equal(10 + round / 2, present.FeedbackData!.Value.GetProperty("degrees").GetInt32());
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
                new() { Action = "visual_expansion_answer", Answers = round < 3 ? expected : ["?", "?"] });
            Assert.True(answer.IsValid);
            Assert.Equal(round < 3, answer.IsCorrect);
            var nextState = JsonNode.Parse(session.SessionDataJson)!;
            Assert.Equal(10 + Math.Min(round + 1, 3) / 2, nextState["visualExpansionCurrentDegrees"]!.GetValue<int>());
            Assert.Equal(Math.Min(round + 1, 3), session.CorrectCount);
            Assert.Equal(round == 3 ? 1 : 0, session.IncorrectCount);
            Assert.InRange(answer.FeedbackData!.Value.GetProperty("responseTimeMs").GetInt32(), 490, 1000);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "visual_expansion_answer", Answers = expected })).IsValid);
        }
        var result = await service.CompleteAsync(student, started.SessionId, new() { CustomData = new() { ["maxDegreesReached"] = System.Text.Json.JsonSerializer.SerializeToElement(999) } });
        Assert.Equal(75, result.Accuracy);
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        Assert.Equal(11, result.DetailedResults.GetProperty("visualExpansionMaxPresentedDistance").GetInt32());
        Assert.Equal(4, result.DetailedResults.GetProperty("visualExpansionRoundResults").GetArrayLength());
        var summary = result.DetailedResults.GetProperty("visualExpansionTargetSummary");
        Assert.False(summary.GetProperty("targetReached").GetBoolean());
        Assert.Equal(0, summary.GetProperty("targetRounds").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, summary.GetProperty("targetAccuracy").ValueKind);
        db.ChangeTracker.Clear();
        var repeat = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Equal(75, repeat.Accuracy);
        Assert.Equal(4, repeat.DetailedResults.GetProperty("visualExpansionRoundResults").GetArrayLength());
        Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(false, "visual-expansion", "VisualExpansion", "visual_expansion")]
    [InlineData(true, "visual-expansion", "VisualExpansion", "visual_expansion")]
    [InlineData(false, "fixation", "Fixation", "motion_path")]
    [InlineData(true, "fixation", "Fixation", "motion_path")]
    [InlineData(false, "fixation", "Fixation", "motion_path", true)]
    public async Task Catalog_update_is_atomic_idempotent_and_preserves_assessments_and_open_snapshots(bool changedBaseline, string packName, string typeName, string engineType, bool changedEngine = false)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", packName, "v1");
        using var settings = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture owns a disposable database, never a live database.
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, engineType);
            db.ExerciseTypes.Add(type);
            var rows = settings.RootElement.EnumerateArray().ToArray();
            foreach (var row in rows)
                db.Exercises.Add(Exercise.Create("Çevredeki Harfler", "attention",
                    row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    student, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            var assessment = Exercise.Create("Seviye Tespit", "attention", $$"""{"engineType":"{{engineType}}","rounds":6}""", 1, student, type.Id);
            db.Exercises.Add(assessment); await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = rows[0].GetProperty("id").GetGuid() });
            var snapshot = (await db.ExerciseSessions.SingleAsync()).SessionDataJson;
            if (changedBaseline || changedEngine) {
                if (changedBaseline)
                    await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.exercises SET configuration_json='{{}}'::jsonb WHERE id={0}", rows[0].GetProperty("id").GetGuid());
                else
                    await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.exercise_types SET engine_type='text_stream' WHERE id={0}", type.Id);
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecuteAsync(sql));
                db.ChangeTracker.Clear();
                var untouched = await db.Exercises.SingleAsync(item => item.Id == rows[1].GetProperty("id").GetGuid());
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(untouched.ConfigurationJson), JsonNode.Parse(rows[1].GetProperty("previousConfiguration").GetRawText())));
                return;
            }
            await ExecuteAsync(sql); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Version);
            await ExecuteAsync(sql); db.ChangeTracker.Clear();
            Assert.Equal(21, await db.Exercises.CountAsync());
            foreach (var row in rows) {
                var exercise = await db.Exercises.SingleAsync(item => item.Id == row.GetProperty("id").GetGuid());
                Assert.Equal(versions[exercise.Id], exercise.Version);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(exercise.ConfigurationJson), JsonNode.Parse(row.GetProperty("configuration").GetRawText())));
                var fresh = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = exercise.Id });
                var config = row.GetProperty("configuration").GetProperty("engineConfig");
                Assert.Equal(typeName == "Fixation" ? config.GetProperty("content").GetProperty("points").GetInt32()
                    : config.GetProperty("rounds").GetInt32(), fresh.TotalSteps);
                if (typeName == "Fixation") {
                    Assert.Equal(2, fresh.InitialData.GetProperty("fixationProtocolVersion").GetInt32());
                    Assert.Equal(config.GetProperty("content").GetProperty("peripheralCount").GetInt32(),
                        fresh.InitialData.GetProperty("fixationPeripheralCount").GetInt32());
                }
            }
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(assessment.ConfigurationJson),
                JsonNode.Parse((await db.Exercises.SingleAsync(item => item.Id == assessment.Id)).ConfigurationJson)));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(snapshot),
                JsonNode.Parse((await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId)).SessionDataJson)));
            var resumed = await service.StartAsync(student, new() { ExerciseId = rows[0].GetProperty("id").GetGuid() });
            Assert.Equal(started.SessionId, resumed.SessionId);
            Assert.Equal(started.TotalSteps, resumed.TotalSteps);
        } finally { await db.Database.EnsureDeletedAsync(); }

        async Task ExecuteAsync(string script) {
            await using var connection = new Npgsql.NpgsqlConnection(postgres.ConnectionString);
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
