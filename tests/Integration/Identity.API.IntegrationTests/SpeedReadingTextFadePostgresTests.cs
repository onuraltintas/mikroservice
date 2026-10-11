using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingTextFadePostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("valid")]
    [InlineData("changed")]
    [InlineData("missing-questions")]
    public async Task Catalog_pack_requires_four_questions_is_atomic_and_idempotent(string scenario)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "text-fading", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database supplied by PostgresFixture.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "TextFading", "Metin Solma", "text_fade");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                if (row.GetProperty("previousConfiguration").ValueKind != JsonValueKind.Null)
                    db.Exercises.Add(Exercise.Create(row.GetProperty("previousTitle").GetString()!, "reading",
                        scenario == "changed" ? "{}" : row.GetProperty("previousConfiguration").GetRawText(),
                        row.GetProperty("level").GetInt32(), student, type.Id, row.GetProperty("id").GetGuid(),
                        targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
                var text = ReadingText.Create(Guid.NewGuid(), "Gerçek test metni", "bir iki üç dört beş altı",
                    difficultyLevel: row.GetProperty("level").GetInt32(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid());
                db.ReadingTexts.Add(text);
                for (var index = 0; index < (scenario == "missing-questions" ? 3 : 5); index++)
                    db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, $"Soru {index}", "A", index,
                        type: 1, bloomLevel: 1, optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            }
            await db.SaveChangesAsync();
            var originals = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.ConfigurationJson);
            if (scenario != "valid")
            {
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecutePackAsync(sql));
                Assert.Equal(11, await db.Exercises.CountAsync());
                foreach (var exercise in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(originals[exercise.Id], exercise.ConfigurationJson);
                return;
            }
            // Keep an open old session across the catalog update.
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var oldExercise = await db.Exercises.FirstAsync();
            var oldStart = await service.StartAsync(student, new() { ExerciseId = oldExercise.Id });
            var oldSnapshot = (await db.ExerciseSessions.SingleAsync()).SessionDataJson;
            await ExecutePackAsync(sql);
            db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Version);
            await ExecutePackAsync(sql);
            db.ChangeTracker.Clear();
            Assert.Equal(20, await db.Exercises.CountAsync());
            foreach (var exercise in await db.Exercises.ToListAsync()) Assert.Equal(versions[exercise.Id], exercise.Version);
            Assert.Equal(oldStart.SessionId, (await service.StartAsync(student, new() { ExerciseId = oldExercise.Id })).SessionId);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(oldSnapshot), JsonNode.Parse((await db.ExerciseSessions.SingleAsync()).SessionDataJson)));
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var started = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.Equal(4, started.InitialData.GetProperty("questions").GetArrayLength());
                Assert.Equal(row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("fading").GetProperty("speedWpm").GetInt32(),
                    started.InitialData.GetProperty("fadeDisplayPaceWpm").GetDecimal());
                Assert.Equal(0, started.InitialData.GetProperty("readingMaximumMs").GetInt32());
            }
            Assert.Equal(20, await db.ReadingTexts.CountAsync());
            Assert.Equal(100, await db.ReadingQuestions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task ExecutePackAsync(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Persists_four_question_evaluation_without_measured_reading_speed()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "TextFading", "Metin Solma", "text_fade");
            var exercise = Exercise.Create("Metin Solma", "reading",
                """{"engineType":"text_fade","engineConfig":{"readingPurpose":"evaluation","fading":{"speedWpm":200,"lagMs":0}}}""",
                3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            var text = ReadingText.Create(Guid.NewGuid(), "Metin", "bir iki üç dört", difficultyLevel: 3);
            db.ReadingTexts.Add(text);
            for (var index = 0; index < 5; index++)
                db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, $"Soru {index}", "A", index,
                    type: 1, bloomLevel: 1, optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(4, started.InitialData.GetProperty("questions").GetArrayLength());
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(-1300);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
            var questions = started.InitialData.GetProperty("questions").EnumerateArray().ToArray();
            for (var index = 0; index < questions.Length; index++)
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() {
                    Action = "answer_question", QuestionId = questions[index].GetProperty("questionId").GetGuid(),
                    Answer = index == 3 ? "B" : "A", ResponseTime = 1000 })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(75, result.ComprehensionScore);
            Assert.Null(result.RawWPM);
            Assert.Equal(200, result.DetailedResults.GetProperty("fadeDisplayPaceWpm").GetDecimal());
            db.ChangeTracker.Clear();
            Assert.Equal(75, (await service.CompleteAsync(student, started.SessionId, new())).ComprehensionScore);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persists_verified_full_and_partial_fade_without_fabricated_metrics(bool partial)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // PostgresFixture owns a disposable Testcontainers database, never a live database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "TextFading", "Metin Solma", "text_fade");
            var config = new JsonObject {
                ["engineType"] = "text_fade", ["engineConfig"] = new JsonObject {
                    ["fading"] = new JsonObject { ["speedWpm"] = 200, ["lagMs"] = 300 },
                    ["timing"] = new JsonObject { ["timeLimitSec"] = partial ? 1 : 0 }
                }
            };
            var exercise = Exercise.Create("Metin Solma", "reading", config.ToJsonString(), 3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "B", "beş altı yedi sekiz", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(1500, started.InitialData.GetProperty("readingMinimumMs").GetInt32());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            await service.PauseAsync(student, started.SessionId);
            await service.ResumeAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync();
            var textId = session.ReadingTextId;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -1100 : -1600);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            // Even a client claiming full completion cannot bypass the server deadline.
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM);
            Assert.Null(result.Accuracy);
            Assert.Null(result.ComprehensionScore);
            Assert.Equal(0, result.XpGained);
            Assert.Null(result.WordsRead);
            Assert.Equal(partial, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            Assert.Equal(200, result.DetailedResults.GetProperty("fadeDisplayPaceWpm").GetDecimal());
            Assert.Equal(partial ? 50 : 100, result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal());
            db.ChangeTracker.Clear();
            var repeat = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeat.RawWPM);
            Assert.Equal(result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal(), repeat.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id != started.SessionId)).ReadingTextId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
