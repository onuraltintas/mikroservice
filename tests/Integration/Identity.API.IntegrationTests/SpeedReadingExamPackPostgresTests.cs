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
public sealed class SpeedReadingExamPackPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("none")]
    [InlineData("catalog")]
    [InlineData("content")]
    public async Task PackageIsAtomicIdempotentRunsAllAgesAndPreservesHistoryOnRollback(string drift)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var pack = Path.Combine(root!.FullName, "content-packs", "exam-simulation", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var apply = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var owner = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "ExamSimulation", "Süreli Paragraf Testi", "exam_simulation");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var exercise = row.GetProperty("id").GetGuid(); var age = row.GetProperty("ageGroupId").GetGuid();
                db.Exercises.Add(Exercise.Create("Sınav", "ExamSimulation", drift == "catalog" ? "{}" : row.GetProperty("previousConfiguration").GetRawText(),
                    5, owner, type.Id, exercise, targetAgeGroupId: age));
                var content = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("content");
                var length = content.GetProperty("minWordCount").GetInt32() + 10;
                for (var index = 0; index < (drift == "content" ? 2 : 3); index++)
                {
                    var text = ReadingText.Create(Guid.NewGuid(), "Test paragrafı " + index, string.Join(' ', Enumerable.Repeat("okuma", length)),
                        difficultyLevel: 5, targetAgeGroupId: age, exerciseId: exercise);
                    db.ReadingTexts.Add(text);
                    for (var number = 0; number < 9; number++) db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id,
                        "Metne ilişkin soru " + number, "A", number, 1, 2, 5, optionA: "Okuma", optionB: "Ulaşım", optionC: "Spor", optionD: "Hava"));
                }
            }
            var history = ExerciseSession.Start(owner, settings.RootElement[0].GetProperty("id").GetGuid(), null, 3, DateTime.UtcNow, null);
            history.SetState("{\"oldSnapshot\":true}"); db.ExerciseSessions.Add(history);
            await db.SaveChangesAsync();
            var before = await db.Exercises.AsNoTracking().ToDictionaryAsync(row => row.Id, row => row.ConfigurationJson);
            if (drift != "none")
            {
                await Assert.ThrowsAsync<PostgresException>(() => Execute(apply));
                foreach (var row in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(before[row.Id], row.ConfigurationJson);
                return;
            }
            await Execute(apply); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(row => row.Id, row => row.Version);
            await Execute(apply); db.ChangeTracker.Clear();
            foreach (var row in await db.Exercises.ToListAsync()) Assert.Equal(versions[row.Id], row.Version);
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid();
                var start = await service.StartAsync(user, new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.Equal(8, start.InitialData.GetProperty("questions").GetArrayLength());
                Assert.All(start.InitialData.GetProperty("questions").EnumerateArray(), question => Assert.False(question.TryGetProperty("correctAnswer", out _)));
                await service.ValidateActionAsync(user, start.SessionId, new() { Action = "start_reading" });
                var session = await db.ExerciseSessions.SingleAsync(item => item.Id == start.SessionId);
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-15); session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                foreach (var question in start.InitialData.GetProperty("questions").EnumerateArray())
                {
                    var id = question.GetProperty("questionId").GetGuid();
                    Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "exam_question_start", QuestionId = id })).IsValid);
                    var answer = await service.ValidateActionAsync(user, session.Id, new() { Action = "answer_question", QuestionId = id, Answer = "A" });
                    Assert.True(answer.IsValid); Assert.Null(answer.IsCorrect); Assert.Null(answer.CorrectAnswer);
                }
                var result = await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(100, result.ComprehensionScore); Assert.Null(result.RawWPM);
                Assert.Equal(8, result.DetailedResults.GetProperty("questions").GetArrayLength());
                await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(1, await db.ExerciseSessionResults.CountAsync(item => item.SessionId == session.Id));
            }
            await Execute(await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql"))); db.ChangeTracker.Clear();
            foreach (var row in await db.Exercises.ToListAsync()) Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before[row.Id]), JsonNode.Parse(row.ConfigurationJson)));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"oldSnapshot\":true}"),
                JsonNode.Parse((await db.ExerciseSessions.SingleAsync(row => row.Id == history.Id)).SessionDataJson)));
            Assert.Equal(3, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(9, await db.ReadingTexts.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
        async Task Execute(string sql)
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
        }
    }
}
