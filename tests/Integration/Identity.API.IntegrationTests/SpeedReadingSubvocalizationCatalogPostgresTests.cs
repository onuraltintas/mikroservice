using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingSubvocalizationCatalogPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("none")]
    [InlineData("catalog")]
    [InlineData("program")]
    [InlineData("question")]
    [InlineData("unsnapshotted")]
    public async Task Pack_is_atomic_idempotent_and_preserves_owned_schedules(string changedSource)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "subvocalization", "v2");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        using var programs = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "programs.json")));
        using var content = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "content.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database is recreated here.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "SubvocalizationReduction", "Regresyon", "subvocalization_reduction");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray().Where(row => row.GetProperty("previousConfiguration").ValueKind != JsonValueKind.Null))
                db.Exercises.Add(Exercise.Create(row.GetProperty("previousTitle").GetString()!, "reading",
                    changedSource == "catalog" ? "{}" : row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    creator, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            foreach (var row in programs.RootElement.EnumerateArray())
            {
                var template = ProgramTemplate.Import(row.GetProperty("id").GetGuid(), "Program", "Test",
                    row.GetProperty("ageGroupId").GetGuid(), 0, 100,
                    changedSource == "program" ? "{}" : row.GetProperty("previousPattern").GetRawText(),
                    row.GetProperty("level").GetInt32(), 0, 5, 4, 28, true, 1, 0, null, false, DateTime.UtcNow, "test", null, null);
                db.ProgramTemplates.Add(template);
                var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, creator, DateTime.UtcNow);
                if (changedSource != "unsnapshotted")
                    progress.SetSchedule("[{\"WeekNumber\":1,\"DayNumber\":1,\"Order\":1}]", creator, DateTime.UtcNow);
                db.StudentProgramProgresses.Add(progress);
                foreach (var task in row.GetProperty("pattern").EnumerateObject().Where(week => week.Name.StartsWith("week"))
                    .SelectMany(week => week.Value.EnumerateObject()).SelectMany(day => day.Value.EnumerateArray()))
                {
                    var name = task.GetProperty("type").GetString()!;
                    if (name == "SubvocalizationReduction") continue;
                    var other = db.ExerciseTypes.Local.SingleOrDefault(item => item.Name == name);
                    if (other is null) { other = ExerciseType.Create(Guid.NewGuid(), name, name, "focus"); db.ExerciseTypes.Add(other); }
                    var level = task.GetProperty("difficulty").GetInt32(); var age = row.GetProperty("ageGroupId").GetGuid();
                    if (!db.Exercises.Local.Any(item => item.ExerciseTypeId == other.Id && item.DifficultyLevel == level && item.TargetAgeGroupId == age))
                        db.Exercises.Add(Exercise.Create(name, "test", "{}", level, creator, other.Id, targetAgeGroupId: age));
                }
            }
            if (changedSource == "question")
            {
                var existingId = settings.RootElement.EnumerateArray().First(row => row.GetProperty("previousConfiguration").ValueKind != JsonValueKind.Null).GetProperty("id").GetGuid();
                var text = content.RootElement.EnumerateArray().First(row => row.GetProperty("exerciseId").GetGuid() == existingId);
                var question = text.GetProperty("questions")[0];
                db.ReadingTexts.Add(ReadingText.Create(text.GetProperty("id").GetGuid(), text.GetProperty("title").GetString()!,
                    text.GetProperty("content").GetString()!, difficultyLevel: text.GetProperty("level").GetInt32(),
                    targetAgeGroupId: text.GetProperty("ageGroupId").GetGuid(), exerciseId: text.GetProperty("exerciseId").GetGuid()));
                db.ReadingQuestions.Add(ReadingQuestion.Create(question.GetProperty("id").GetGuid(), text.GetProperty("id").GetGuid(),
                    "Edited question", "A", 0, type: 1, bloomLevel: 1, optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            }
            await db.SaveChangesAsync();
            var schedules = await db.StudentProgramProgresses.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.ScheduleJson);
            if (changedSource != "none")
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => ExecutePack(sql));
                Assert.Equal(changedSource switch {
                    "catalog" => "Subvocalization catalog baseline changed",
                    "program" => "Subvocalization program baseline changed",
                    "unsnapshotted" => "Subvocalization existing active program needs schedule snapshot",
                    _ => "Subvocalization question collision or later edit"
                }, error.MessageText);
                Assert.Equal(6, await db.Exercises.CountAsync(item => item.ExerciseTypeId == type.Id));
                Assert.Equal(changedSource == "question" ? 1 : 0, await db.ReadingQuestions.CountAsync());
                return;
            }
            await ExecutePack(sql); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Version);
            await ExecutePack(sql); db.ChangeTracker.Clear();
            Assert.Equal(13, await db.Exercises.CountAsync(item => item.ExerciseTypeId == type.Id));
            Assert.Equal(39, await db.ReadingTexts.CountAsync()); Assert.Equal(195, await db.ReadingQuestions.CountAsync());
            foreach (var progress in await db.StudentProgramProgresses.ToListAsync()) Assert.Equal(schedules[progress.Id], progress.ScheduleJson);
            foreach (var exercise in await db.Exercises.ToListAsync()) Assert.Equal(versions[exercise.Id], exercise.Version);
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in programs.RootElement.EnumerateArray())
            {
                var template = await db.ProgramTemplates.SingleAsync(item => item.Id == row.GetProperty("id").GetGuid());
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(template.WeeklyPatternJson), JsonNode.Parse(row.GetProperty("pattern").GetRawText())));
                var scheduleType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
                var build = scheduleType.GetMethod("BuildAsync", BindingFlags.Static | BindingFlags.Public)!;
                using var schedule = JsonDocument.Parse(await (Task<string>)build.Invoke(null, [db, template, null, CancellationToken.None])!);
                Assert.Equal(182, schedule.RootElement.GetArrayLength());
                var exerciseId = settings.RootElement.EnumerateArray().Single(setting =>
                    setting.GetProperty("ageGroupId").GetGuid() == row.GetProperty("ageGroupId").GetGuid()
                    && setting.GetProperty("level").GetInt32() == row.GetProperty("level").GetInt32()).GetProperty("id").GetGuid();
                Assert.Equal(2, schedule.RootElement.EnumerateArray().Count(task => task.GetProperty("ExerciseId").GetGuid() == exerciseId));
                var student = Guid.NewGuid();
                var started = await service.StartAsync(student, new() { ExerciseId = exerciseId });
                Assert.Equal(5, started.InitialData.GetProperty("questions").GetArrayLength());
                Assert.DoesNotContain("correctAnswer", started.InitialData.ToString(), StringComparison.OrdinalIgnoreCase);
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
                var session = await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-300);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
                foreach (var question in started.InitialData.GetProperty("questions").EnumerateArray())
                {
                    var id = question.GetProperty("questionId").GetGuid();
                    var answer = (await db.ReadingQuestions.SingleAsync(item => item.Id == id)).CorrectAnswer;
                    Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "answer_question", QuestionId = id, Answer = answer })).IsCorrect);
                }
                var result = await service.CompleteAsync(student, started.SessionId, new());
                Assert.Null(result.RawWPM); Assert.Equal(100m, result.ComprehensionScore);
                await service.CompleteAsync(student, started.SessionId, new());
                Assert.Equal(1, await db.ExerciseSessionResults.CountAsync(item => item.SessionId == started.SessionId));
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task ExecutePack(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(); await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
