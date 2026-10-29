using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingGroupingPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_pack_is_atomic_idempotent_and_preserves_active_schedules(bool changedSource)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "grouping", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        using var programs = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "programs.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Chunking", "Gruplama", "word_highlight");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray().Where(row => row.GetProperty("level").GetInt32() < 5))
                db.Exercises.Add(Exercise.Create(row.GetProperty("previousTitle").GetString()!, "reading",
                    changedSource ? "{}" : row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    student, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            foreach (var row in programs.RootElement.EnumerateArray())
            {
                var template = ProgramTemplate.Import(row.GetProperty("id").GetGuid(), "Program", "Test",
                    row.GetProperty("ageGroupId").GetGuid(), 0, 100, row.GetProperty("previousPattern").GetRawText(),
                    5, 0, 5, 4, 28, true, 1, 0, null, false, DateTime.UtcNow, "test", null, null);
                db.ProgramTemplates.Add(template);
                var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, student, DateTime.UtcNow);
                progress.SetSchedule("[{\"WeekNumber\":1,\"DayNumber\":2,\"Order\":4,\"ExerciseId\":\"" +
                    settings.RootElement[3].GetProperty("id").GetString() + "\"}]", student, DateTime.UtcNow);
                db.StudentProgramProgresses.Add(progress);
                foreach (var name in row.GetProperty("pattern").EnumerateObject().Where(week => week.Name.StartsWith("week"))
                    .SelectMany(week => week.Value.EnumerateObject()).SelectMany(day => day.Value.EnumerateArray())
                    .Select(task => task.GetProperty("type").GetString()!).Distinct().Where(name => name != "Chunking"))
                {
                    var otherType = db.ExerciseTypes.Local.SingleOrDefault(item => item.Name == name);
                    if (otherType is null)
                    {
                        otherType = ExerciseType.Create(Guid.NewGuid(), name, name, "focus");
                        db.ExerciseTypes.Add(otherType);
                    }
                    db.Exercises.Add(Exercise.Create(name, "test", "{}", 5, student, otherType.Id,
                        targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
                }
            }
            await db.SaveChangesAsync();
            var schedules = await db.StudentProgramProgresses.AsNoTracking().ToDictionaryAsync(progress => progress.Id, progress => progress.ScheduleJson);
            if (changedSource)
            {
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecutePackAsync(sql));
                Assert.Equal(16, await db.Exercises.CountAsync(exercise => exercise.ExerciseTypeId == type.Id));
                Assert.Empty(await db.ReadingTexts.ToListAsync());
                return;
            }
            await ExecutePackAsync(sql);
            db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(exercise => exercise.Id, exercise => exercise.Version);
            await ExecutePackAsync(sql);
            db.ChangeTracker.Clear();
            Assert.Equal(20, await db.Exercises.CountAsync(exercise => exercise.ExerciseTypeId == type.Id));
            Assert.Equal(60, await db.ReadingTexts.CountAsync());
            foreach (var progress in await db.StudentProgramProgresses.ToListAsync()) Assert.Equal(schedules[progress.Id], progress.ScheduleJson);
            foreach (var exercise in await db.Exercises.ToListAsync()) Assert.Equal(versions[exercise.Id], exercise.Version);
            foreach (var row in programs.RootElement.EnumerateArray())
            {
                var template = await db.ProgramTemplates.SingleAsync(template => template.Id == row.GetProperty("id").GetGuid());
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(template.WeeklyPatternJson), JsonNode.Parse(row.GetProperty("pattern").GetRawText())));
                var scheduleType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
                var build = scheduleType.GetMethod("BuildAsync", BindingFlags.Static | BindingFlags.Public)!;
                var scheduleJson = await (Task<string>)build.Invoke(null, [db, template, null, CancellationToken.None])!;
                using var schedule = JsonDocument.Parse(scheduleJson);
                Assert.Equal(182, schedule.RootElement.GetArrayLength());
                var chunkingIds = (await db.Exercises.Where(exercise => exercise.ExerciseTypeId == type.Id && exercise.DifficultyLevel == 5)
                    .Select(exercise => exercise.Id).ToListAsync()).ToHashSet();
                Assert.Equal(16, schedule.RootElement.EnumerateArray().Count(task => chunkingIds.Contains(task.GetProperty("ExerciseId").GetGuid())));
            }
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var started = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.Equal(1, started.InitialData.GetProperty("groupingVersion").GetInt32());
                Assert.Empty(started.InitialData.GetProperty("questions").EnumerateArray());
                Assert.Equal(0, started.InitialData.GetProperty("readingMaximumMs").GetInt32());
                Assert.Equal(row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("pacer").GetProperty("speedWpm").GetInt32(),
                    started.InitialData.GetProperty("groupingDisplayPaceWpm").GetDecimal());
            }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persists_complete_and_partial_grouping_without_fabricated_reading_metrics(bool partial)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture owns an isolated disposable Testcontainers database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Chunking", "Gruplama", "word_highlight");
            var exercise = Exercise.Create("Gruplama", "reading",
                """{"engineType":"word_highlight","engineConfig":{"mode":"chunking","pacer":{"chunkSize":2,"speedWpm":200}}}""",
                3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "B", "beş altı yedi sekiz", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            await service.PauseAsync(student, started.SessionId);
            await service.ResumeAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync();
            var textId = session.ReadingTextId;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -600 : -2000);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading", IsTimeout = partial })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM);
            Assert.Null(result.Accuracy);
            Assert.Null(result.ComprehensionScore);
            Assert.Equal(0, result.XpGained);
            Assert.Equal(partial, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            Assert.Equal(200, result.DetailedResults.GetProperty("groupingDisplayPaceWpm").GetDecimal());
            var completion = result.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal();
            Assert.InRange(completion, partial ? 40 : 100, partial ? 80 : 100);
            db.ChangeTracker.Clear();
            var repeat = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeat.RawWPM);
            Assert.Equal(completion, repeat.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id != started.SessionId)).ReadingTextId);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
