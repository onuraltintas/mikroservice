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
public sealed class SpeedReadingComprehensionContentPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("none")]
    [InlineData("catalog")]
    [InlineData("text")]
    [InlineData("question")]
    [InlineData("insert")]
    public async Task Content_pack_is_atomic_idempotent_and_runs_all_forty_modes(string changedSource)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "comprehension", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        using var content = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "content.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This is exclusively the disposable Testcontainers database, never a production connection.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create(row.GetProperty("previousTitle").GetString()!, "reading",
                    changedSource == "catalog" ? "{}" : row.GetProperty("previousConfiguration").GetRawText(),
                    row.GetProperty("level").GetInt32(), creator, type.Id, row.GetProperty("id").GetGuid(),
                    targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            var placement = Exercise.Create("Seviye tespiti", "assessment", "{\"assessment\":true}", 1, creator, type.Id);
            db.Exercises.Add(placement);
            var oldText = ReadingText.Create(Guid.NewGuid(), "Korunacak metin", "Eski kayıt değişmeden kalır.", difficultyLevel: 1);
            db.ReadingTexts.Add(oldText);
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), oldText.Id, "Eski soru", "A", 0,
                optionA: "Eski kayıt", optionB: "Yeni kayıt", optionC: "Silme", optionD: "Başka kayıt"));
            var open = ExerciseSession.Start(creator, placement.Id, null, 1, DateTime.UtcNow, null);
            open.SetState("{\"oldSnapshot\":true}");
            db.ExerciseSessions.Add(open);
            if (changedSource is "text" or "question")
            {
                var text = content.RootElement[0];
                db.ReadingTexts.Add(ReadingText.Create(text.GetProperty("id").GetGuid(),
                    text.GetProperty("title").GetString()!, changedSource == "text" ? "Yönetici değiştirdi." : text.GetProperty("content").GetString()!,
                    category: "comprehension", difficultyLevel: text.GetProperty("level").GetInt32(),
                    targetAgeGroupId: text.GetProperty("ageGroupId").GetGuid(), exerciseId: text.GetProperty("exerciseId").GetGuid()));
                if (changedSource == "question")
                    db.ReadingQuestions.Add(ReadingQuestion.Create(text.GetProperty("questions")[0].GetProperty("id").GetGuid(),
                        text.GetProperty("id").GetGuid(), "Yönetici soruyu değiştirdi.", "A", 0,
                        optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"));
            }
            await db.SaveChangesAsync();
            var before = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.ConfigurationJson);
            var oldQuestions = await db.ReadingQuestions.CountAsync();
            var oldTexts = await db.ReadingTexts.CountAsync();
            if (changedSource == "insert")
                await ExecuteSql("""
                    CREATE FUNCTION speed_reading.reject_comprehension_insert() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN RAISE EXCEPTION 'Comprehension injected insert failure'; END $$;
                    CREATE TRIGGER reject_comprehension_insert BEFORE INSERT ON speed_reading.reading_questions
                    FOR EACH ROW EXECUTE FUNCTION speed_reading.reject_comprehension_insert();
                    """);
            if (changedSource != "none")
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSql(sql));
                Assert.Contains("Comprehension", error.MessageText);
                Assert.Equal(oldTexts, await db.ReadingTexts.CountAsync());
                Assert.Equal(oldQuestions, await db.ReadingQuestions.CountAsync());
                foreach (var e in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(before[e.Id], e.ConfigurationJson);
                return;
            }
            await ExecuteSql(sql); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Id, e => (e.Version, e.UpdatedAt));
            await ExecuteSql(sql); db.ChangeTracker.Clear();
            Assert.Equal(81, await db.ReadingTexts.CountAsync());
            Assert.Equal(589, await db.ReadingQuestions.CountAsync());
            Assert.Equal("Eski kayıt değişmeden kalır.", (await db.ReadingTexts.SingleAsync(t => t.Id == oldText.Id)).Content);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"oldSnapshot\":true}"),
                JsonNode.Parse((await db.ExerciseSessions.SingleAsync(s => s.Id == open.Id)).SessionDataJson)));
            foreach (var e in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(versions[e.Id], (e.Version, e.UpdatedAt));
            Assert.Equal(before[placement.Id], (await db.Exercises.SingleAsync(e => e.Id == placement.Id)).ConfigurationJson);
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid();
                var exerciseId = row.GetProperty("id").GetGuid();
                var expected = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("content");
                var mode = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("comprehensionMode").GetString();
                var started = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(exerciseId, text.ExerciseId);
                Assert.Equal(row.GetProperty("level").GetInt32(), text.DifficultyLevel);
                Assert.Equal(row.GetProperty("ageGroupId").GetGuid(), text.TargetAgeGroupId);
                Assert.InRange(text.WordCount, expected.GetProperty("minWordCount").GetInt32(), expected.GetProperty("maxWordCount").GetInt32());
                Assert.Equal(expected.GetProperty("questionCount").GetInt32(), started.InitialData.GetProperty("questions").GetArrayLength());
                Assert.All(started.InitialData.GetProperty("questions").EnumerateArray(), q => Assert.False(q.TryGetProperty("correctAnswer", out _)));
                await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
                await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddMinutes(-3);
                session.SetState(state.ToJsonString());
                await db.SaveChangesAsync();
                await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
                foreach (var question in started.InitialData.GetProperty("questions").EnumerateArray())
                {
                    var id = question.GetProperty("questionId").GetGuid();
                    var answer = (await db.ReadingQuestions.SingleAsync(q => q.Id == id)).CorrectAnswer;
                    var validated = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "answer_question", QuestionId = id, Answer = answer });
                    Assert.Equal(mode == "learning" ? true : null, validated.IsCorrect);
                }
                var result = await service.CompleteAsync(user, started.SessionId, new());
                Assert.Equal(100m, result.Score);
                Assert.Equal(mode, result.DetailedResults.GetProperty("comprehensionMode").GetString());
                Assert.Equal(mode == "learning", result.RawWPM is null);
                await service.CompleteAsync(user, started.SessionId, new());
                Assert.Equal(1, await db.ExerciseSessionResults.CountAsync(r => r.SessionId == started.SessionId));
                var second = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                Assert.NotEqual(session.ReadingTextId, (await db.ExerciseSessions.SingleAsync(s => s.Id == second.SessionId)).ReadingTextId);
            }
            Assert.Equal(40, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(20, await db.ReadingSessions.CountAsync());
            // A scoped rollback after real results exist must archive content, not erase history.
            var rollback = await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql"));
            await ExecuteSql(rollback); db.ChangeTracker.Clear();
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var exercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == row.GetProperty("id").GetGuid());
                Assert.Equal(row.GetProperty("previousTitle").GetString(), exercise.Title);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("previousConfiguration").GetRawText()),
                    JsonNode.Parse(exercise.ConfigurationJson)));
            }
            Assert.Equal(1, await db.ReadingTexts.CountAsync(t => t.IsActive));
            Assert.Equal(589, await db.ReadingQuestions.CountAsync());
            Assert.Equal(40, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(20, await db.ReadingSessions.CountAsync());
            var rollbackVersions = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Id, e => (e.Version, e.UpdatedAt));
            var textVersions = await db.ReadingTexts.AsNoTracking().ToDictionaryAsync(t => t.Id, t => (t.Version, t.UpdatedAt));
            await ExecuteSql(rollback); db.ChangeTracker.Clear();
            foreach (var e in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(rollbackVersions[e.Id], (e.Version, e.UpdatedAt));
            foreach (var text in await db.ReadingTexts.AsNoTracking().ToListAsync())
            {
                Assert.Equal(textVersions[text.Id], (text.Version, text.UpdatedAt));
                Assert.Equal(text.Id == oldText.Id, text.IsActive);
            }
            var firstTextId = content.RootElement[0].GetProperty("id").GetGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE speed_reading.reading_texts SET title='Sonradan düzenlendi' WHERE id={firstTextId}");
            var rollbackError = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSql(rollback));
            Assert.Contains("content changed", rollbackError.MessageText);
            var originalTitle = content.RootElement[0].GetProperty("title").GetString()!;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE speed_reading.reading_texts SET title={originalTitle}, language='en' WHERE id={firstTextId}");
            var metadataError = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSql(rollback));
            Assert.Contains("content changed", metadataError.MessageText);
            Assert.Equal(40, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task ExecuteSql(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
