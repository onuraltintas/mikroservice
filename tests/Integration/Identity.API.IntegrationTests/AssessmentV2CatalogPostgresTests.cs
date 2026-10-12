using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class AssessmentV2CatalogPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Catalog_is_atomic_idempotent_reversible_and_pins_age_specific_questions_with_first_reading_speed()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try {
        await db.Database.EnsureCreatedAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "assessment", "v2");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var rows = settings.RootElement.EnumerateArray().ToArray();
        var actor = Guid.NewGuid();
        var types = rows.Select(row => row.GetProperty("type").GetString()!).Distinct().ToDictionary(name => name,
            name => ExerciseType.Create(Guid.NewGuid(), name, name, name == "Tachistoscope" ? "text_stream" : name == "VisualExpansion" ? "visual_expansion" : "reading_comprehension"));
        db.ExerciseTypes.AddRange(types.Values);
        foreach (var row in rows)
            db.Exercises.Add(Exercise.Create("Assessment", row.GetProperty("type").GetString()!,
                row.GetProperty("previousConfiguration").GetRawText(), 1, actor, types[row.GetProperty("type").GetString()!].Id,
                row.GetProperty("id").GetGuid(), description: row.GetProperty("previousDescription").GetString(),
                targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid(), isAssessmentOnly: true));
        foreach (var age in rows.Select(row => row.GetProperty("ageGroupId").GetGuid()).Distinct())
        {
            var plan = JsonSerializer.Serialize(new { week1 = rows.Where(row => row.GetProperty("ageGroupId").GetGuid() == age)
                .OrderBy(row => row.GetProperty("type").GetString() == "Comprehension" ? 0 : row.GetProperty("type").GetString() == "Tachistoscope" ? 1 : 2)
                .Select((row, index) => new { exerciseId = row.GetProperty("id").GetGuid(), displayOrder = index + 1 }) });
            db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "Test", age, 0, 100, plan,
                1, 0, 1, 1, 1, true, 0, 0, null, true, DateTime.UtcNow, actor.ToString(), null, null));
            var text = ReadingText.Create(Guid.NewGuid(), "Age text", string.Join(' ', Enumerable.Repeat("kelime", 80)), difficultyLevel: 1, targetAgeGroupId: age);
            db.ReadingTexts.Add(text);
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Invalid metadata", "A", 0,
                type: 0, bloomLevel: 0, optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Missing option", "A", 1,
                type: 1, bloomLevel: 1, optionA: "", optionB: "B", optionC: "C", optionD: "D"));
            for (var index = 0; index < 10; index++)
                db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru", "A", index + 2, 1, 1,
                    optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
        }
        await db.SaveChangesAsync();
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        var rollback = await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql"));
        await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.exercises SET description='Admin edit' WHERE id={0}", rows[0].GetProperty("id").GetGuid());
        await Assert.ThrowsAsync<PostgresException>(() => Execute(sql));
        await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.exercises SET description={0} WHERE id={1}",
            rows[0].GetProperty("previousDescription").GetString()!, rows[0].GetProperty("id").GetGuid());
        await Execute(sql); db.ChangeTracker.Clear();
        var versions = await db.Exercises.ToDictionaryAsync(e => e.Id, e => e.Version);
        await Execute(sql); db.ChangeTracker.Clear();
        Assert.Equal(versions, await db.Exercises.ToDictionaryAsync(e => e.Id, e => e.Version));
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var sessions = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var assessmentType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var assessment = Activator.CreateInstance(assessmentType, db, null)!;
        var pin = assessmentType.GetMethod("BuildPinnedFormItemsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var row in rows.Where(row => row.GetProperty("type").GetString() == "Comprehension"))
        {
            var age = row.GetProperty("ageGroupId").GetGuid();
            var student = Guid.NewGuid();
            var attempt = AssessmentAttempt.Start(Guid.NewGuid(), student, AssessmentAttemptPhase.Baseline,
                "tr-baseline-v2", "tr", null, 3, DateTime.UtcNow, actor.ToString());
            db.AssessmentAttempts.Add(attempt);
            var form = await (Task<IReadOnlyList<AssessmentAttemptExercise>>)pin.Invoke(assessment,
                [attempt.Id, 3, AssessmentAttemptPhase.Baseline, "tr-baseline-v2", age, DateTime.UtcNow, actor.ToString(), CancellationToken.None])!;
            db.AssessmentAttemptExercises.AddRange(form); await db.SaveChangesAsync();
            var started = await sessions.StartAsync(student, new() { ExerciseId = form[0].ExerciseId, AssessmentAttemptId = attempt.Id });
            var count = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("assessmentQuestionCount").GetInt32();
            Assert.Equal(count, started.InitialData.GetProperty("questions").GetArrayLength());
            Assert.True(started.InitialData.GetProperty("assessmentTextReviewAllowed").GetBoolean());
            Assert.False(started.InitialData.GetProperty("questions")[0].TryGetProperty("correctAnswer", out _));
            Assert.All(started.InitialData.GetProperty("questions").EnumerateArray(), question =>
                Assert.NotEqual("Invalid metadata", question.GetProperty("questionText").GetString()));
            Assert.All(started.InitialData.GetProperty("questions").EnumerateArray(), question =>
                Assert.NotEqual("Missing option", question.GetProperty("questionText").GetString()));
            Assert.True((await sessions.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-20);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            Assert.True((await sessions.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            foreach (var question in started.InitialData.GetProperty("questions").EnumerateArray())
            {
                var answer = await sessions.ValidateActionAsync(student, started.SessionId, new() {
                    Action = "answer_question", QuestionId = question.GetProperty("questionId").GetGuid(), Answer = "A", ResponseTime = 100000 });
                Assert.True(answer.IsValid); Assert.Null(answer.IsCorrect); Assert.Null(answer.CorrectAnswer);
            }
            var completed = await sessions.CompleteAsync(student, started.SessionId, new() { IsAssessmentMode = true });
            Assert.InRange(completed.RawWPM!.Value, 225, 245);
            Assert.Equal(100, completed.ComprehensionScore);
        }
        var snapshots = await db.AssessmentAttemptExercises.ToDictionaryAsync(e => e.Id, e => e.ContentSnapshotJson);
        await Execute(rollback); db.ChangeTracker.Clear();
        Assert.Equal(snapshots, await db.AssessmentAttemptExercises.ToDictionaryAsync(e => e.Id, e => e.ContentSnapshotJson));
        foreach (var row in rows)
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("previousConfiguration").GetRawText()),
                JsonNode.Parse((await db.Exercises.SingleAsync(e => e.Id == row.GetProperty("id").GetGuid())).ConfigurationJson)));
        await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.exercises SET configuration_json='{{}}'::jsonb WHERE id={0}", rows[0].GetProperty("id").GetGuid());
        await Assert.ThrowsAsync<PostgresException>(() => Execute(sql));
        db.ChangeTracker.Clear();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rows[1].GetProperty("previousConfiguration").GetRawText()),
            JsonNode.Parse((await db.Exercises.SingleAsync(e => e.Id == rows[1].GetProperty("id").GetGuid())).ConfigurationJson)));
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
