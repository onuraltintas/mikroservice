using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingSkimmingContentPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("none")]
    [InlineData("catalog")]
    [InlineData("text")]
    [InlineData("question")]
    [InlineData("insert")]
    public async Task Pack_is_atomic_idempotent_runs_twelve_cells_and_rolls_back_without_erasing_history(string drift)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var pack = Path.Combine(root!.FullName, "content-packs", "skimming", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        using var content = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "content.json")));
        var apply = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Exclusively the disposable Testcontainers DB; never a configured application database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Skimming", "Göz Gezdirme", "skimming");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create("Göz Gezdirme", "Skimming", drift == "catalog" ? "{}"
                    : row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    creator, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            var oldText = ReadingText.Create(Guid.NewGuid(), "Korunacak metin", "Eski metin korunur.", difficultyLevel: 1);
            db.ReadingTexts.Add(oldText);
            var oldSession = ExerciseSession.Start(creator, settings.RootElement[0].GetProperty("id").GetGuid(), null, 3, DateTime.UtcNow, null);
            oldSession.SetState("{\"oldSnapshot\":true}"); db.ExerciseSessions.Add(oldSession);
            if (drift is "text" or "question")
            {
                var text = content.RootElement[0];
                db.ReadingTexts.Add(ReadingText.Create(text.GetProperty("id").GetGuid(), text.GetProperty("title").GetString()!,
                    drift == "text" ? "Yönetici değiştirdi." : text.GetProperty("content").GetString()!, category: "skimming",
                    difficultyLevel: text.GetProperty("level").GetInt32(), targetAgeGroupId: text.GetProperty("ageGroupId").GetGuid(),
                    exerciseId: text.GetProperty("exerciseId").GetGuid()));
                if (drift == "question")
                    db.ReadingQuestions.Add(ReadingQuestion.Create(text.GetProperty("questions")[0].GetProperty("id").GetGuid(),
                        text.GetProperty("id").GetGuid(), "Sonradan düzenlenen soru", "A", 0,
                        optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"));
            }
            await db.SaveChangesAsync();
            var before = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.ConfigurationJson);
            var textCount = await db.ReadingTexts.CountAsync();
            var questionCount = await db.ReadingQuestions.CountAsync();
            if (drift == "insert")
                await Execute("""
                    CREATE FUNCTION speed_reading.reject_skimming_insert() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN RAISE EXCEPTION 'Skimming injected insert failure'; END $$;
                    CREATE TRIGGER reject_skimming_insert BEFORE INSERT ON speed_reading.reading_questions
                    FOR EACH ROW EXECUTE FUNCTION speed_reading.reject_skimming_insert();
                    """);
            if (drift != "none")
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(apply));
                Assert.Contains("Skimming", error.MessageText);
                Assert.Equal(textCount, await db.ReadingTexts.CountAsync());
                Assert.Equal(questionCount, await db.ReadingQuestions.CountAsync());
                foreach (var item in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(before[item.Id], item.ConfigurationJson);
                return;
            }
            await Execute(apply); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => (item.Version, item.UpdatedAt));
            await Execute(apply); db.ChangeTracker.Clear();
            Assert.Equal(37, await db.ReadingTexts.CountAsync());
            Assert.Equal(144, await db.ReadingQuestions.CountAsync());
            foreach (var item in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(versions[item.Id], (item.Version, item.UpdatedAt));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"oldSnapshot\":true}"),
                JsonNode.Parse((await db.ExerciseSessions.SingleAsync(item => item.Id == oldSession.Id)).SessionDataJson)));
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid();
                var start = await service.StartAsync(user, new() { ExerciseId = row.GetProperty("id").GetGuid() });
                var session = await db.ExerciseSessions.SingleAsync(item => item.Id == start.SessionId);
                var text = await db.ReadingTexts.SingleAsync(item => item.Id == session.ReadingTextId);
                Assert.Equal(row.GetProperty("id").GetGuid(), text.ExerciseId);
                Assert.Equal(row.GetProperty("ageGroupId").GetGuid(), text.TargetAgeGroupId);
                var config = row.GetProperty("configuration").GetProperty("engineConfig");
                Assert.InRange(text.WordCount, config.GetProperty("content").GetProperty("minWordCount").GetInt32(),
                    config.GetProperty("content").GetProperty("maxWordCount").GetInt32());
                Assert.Equal(4, start.InitialData.GetProperty("questions").GetArrayLength());
                Assert.Equal(config.GetProperty("timing").GetProperty("maxReadingTimeMs").GetInt32(),
                    start.InitialData.GetProperty("readingMaximumMs").GetInt32());
                Assert.All(start.InitialData.GetProperty("questions").EnumerateArray(), question => Assert.False(question.TryGetProperty("correctAnswer", out _)));
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "start_reading" })).IsValid);
                Assert.False((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-30);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                foreach (var question in start.InitialData.GetProperty("questions").EnumerateArray())
                {
                    var id = question.GetProperty("questionId").GetGuid();
                    var answer = (await db.ReadingQuestions.SingleAsync(item => item.Id == id)).CorrectAnswer;
                    Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "answer_question", QuestionId = id, Answer = answer })).IsValid);
                }
                var result = await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(100m, result.ComprehensionScore); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Null(result.WeightedKDP);
                await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(1, await db.ExerciseSessionResults.CountAsync(item => item.SessionId == session.Id));
                var next = await service.StartAsync(user, new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.NotEqual(session.ReadingTextId, (await db.ExerciseSessions.SingleAsync(item => item.Id == next.SessionId)).ReadingTextId);
            }
            var rollback = await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql"));
            await Execute(rollback); db.ChangeTracker.Clear();
            await Execute(rollback); db.ChangeTracker.Clear();
            Assert.Equal(12, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(48, await db.ReadingSessionAnswers.CountAsync());
            Assert.Equal(144, await db.ReadingQuestions.CountAsync());
            Assert.Equal(1, await db.ReadingTexts.CountAsync(item => item.IsActive));
            Assert.Equal("Eski metin korunur.", (await db.ReadingTexts.SingleAsync(item => item.Id == oldText.Id)).Content);
            foreach (var item in await db.Exercises.AsNoTracking().ToListAsync())
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before[item.Id]), JsonNode.Parse(item.ConfigurationJson)));
            await Assert.ThrowsAsync<PostgresException>(() => Execute(apply)); // rollback is not silently undone
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
