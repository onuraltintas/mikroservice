using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class TrainingProgramsV3PostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("normal")]
    [InlineData("changed-pattern")]
    [InlineData("missing-mode")]
    [InlineData("wrong-age")]
    [InlineData("changed-short-settings")]
    public async Task V3_is_atomic_idempotent_preserves_assigned_schedules_and_selects_all_3640_slots(string scenario)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var pack = Path.Combine(root!.FullName, "content-packs/programs/v3");
        using var programs = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "programs.json")));
        using var fading = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "fading-baseline.json")));
        var rows = programs.RootElement.EnumerateArray().ToList();
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var actor = Guid.NewGuid();
            var names = rows.SelectMany(p => p.GetProperty("pattern").EnumerateObject().Where(w => w.Name.StartsWith("week")))
                .SelectMany(w => w.Value.EnumerateObject()).SelectMany(d => d.Value.EnumerateArray())
                .Select(x => x.GetProperty("type").GetString()!).Distinct().ToArray();
            var types = names.ToDictionary(name => name, name => ExerciseType.Create(Guid.NewGuid(), name, name,
                name == "TextFading" ? "text_fade" : name == "Comprehension" ? "reading_comprehension" : name == "Tachistoscope" ? "text_stream" : "focus"));
            db.ExerciseTypes.AddRange(types.Values);
            foreach (var row in rows)
            {
                var age = row.GetProperty("ageId").GetGuid();
                var level = row.GetProperty("level").GetInt32();
                foreach (var name in names)
                {
                    foreach (var mode in name == "Comprehension" ? new[] { "learning", "evaluation" } : new[] { "" })
                    {
                        if (scenario == "missing-mode" && level == 1 && mode == "learning") continue;
                        var source = name == "TextFading" && row.GetProperty("age").GetString() == "Child"
                            ? fading.RootElement.EnumerateArray().Single(f => f.GetProperty("level").GetInt32() == level) : default;
                        var configuration = source.ValueKind == JsonValueKind.Object ? source.GetProperty("configuration").GetRawText()
                            : JsonSerializer.Serialize(new { engineType = types[name].EngineType, engineConfig = new { comprehensionMode = mode } });
                        db.Exercises.Add(Exercise.Create(name + mode, source.ValueKind == JsonValueKind.Object && level <= 3 ? "reading" : name, configuration, level, actor, types[name].Id,
                            source.ValueKind == JsonValueKind.Object ? source.GetProperty("id").GetGuid() : null, targetAgeGroupId: age));
                    }
                }
                db.ProgramTemplates.Add(ProgramTemplate.Import(row.GetProperty("id").GetGuid(), row.GetProperty("name").GetString()!, "",
                    scenario == "wrong-age" && db.ProgramTemplates.Local.Count == 0 ? Guid.NewGuid() : age,
                    0, 100, scenario == "changed-pattern" && db.ProgramTemplates.Local.Count == 0 ? "{}" : row.GetProperty("previousPattern").GetRawText(),
                    level, 0, row.GetProperty("max").GetInt32(), 4, 28, true, level, 0, null, false, DateTime.UtcNow, actor.ToString(), null, null));
            }
            // Install assessment content AFTER the new schema: all 12 records must be scoped.
            foreach (var row in rows.GroupBy(p => p.GetProperty("ageId").GetGuid()).Select(g => g.First()))
                db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "", row.GetProperty("ageId").GetGuid(),
                    0, 100, "{}", 1, 0, 1, 1, 1, true, 0, 0, null, true, DateTime.UtcNow, actor.ToString(), null, null));
            var old = db.ProgramTemplates.Local.First(p => !p.IsAssessment);
            var assigned = StudentProgramProgress.Start(Guid.NewGuid(), actor, old, 0, 0, actor, DateTime.UtcNow);
            assigned.SetSchedule("[{\"WeekNumber\":1,\"DayNumber\":1,\"Order\":1,\"ExerciseId\":\"" + db.Exercises.Local.First().Id + "\"}]", actor, DateTime.UtcNow);
            db.StudentProgramProgresses.Add(assigned);
            await db.SaveChangesAsync();
            await Execute(await File.ReadAllTextAsync(Path.Combine(root.FullName, "content-packs/assessment/v1/apply.sql")));
            Assert.Equal(12, await db.Exercises.CountAsync(e => e.IsAssessmentOnly));
            if (scenario == "changed-short-settings")
                await Execute("UPDATE speed_reading.exercises SET configuration_json='{}' WHERE type_code='TextFading'");
            db.ChangeTracker.Clear();
            var before = await db.ProgramTemplates.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.WeeklyPatternJson);
            var progressBefore = JsonSerializer.Serialize(await db.StudentProgramProgresses.AsNoTracking().SingleAsync());
            var protectedBefore = await Inventory(await File.ReadAllTextAsync(Path.Combine(pack, "inventory-protected.sql")));
            var sql = "BEGIN;\n" + Body(await File.ReadAllTextAsync(Path.Combine(pack, "short-content.sql")))
                + Body(await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"))) + "\nCOMMIT;";
            if (scenario != "normal")
            {
                await Assert.ThrowsAsync<PostgresException>(() => Execute(sql));
                foreach (var p in await db.ProgramTemplates.AsNoTracking().ToListAsync()) Assert.Equal(before[p.Id], p.WeeklyPatternJson);
                Assert.Equal(0, await db.ReadingTexts.CountAsync());
                return;
            }
            await Execute(sql);
            db.ChangeTracker.Clear();
            var versions = await db.ProgramTemplates.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.Version, p.UpdatedAt));
            await Execute(sql);
            db.ChangeTracker.Clear();
            Assert.Equal(progressBefore, JsonSerializer.Serialize(await db.StudentProgramProgresses.AsNoTracking().SingleAsync()));
            Assert.Equal(protectedBefore, await Inventory(await File.ReadAllTextAsync(Path.Combine(pack, "inventory-protected.sql"))));
            foreach (var p in await db.ProgramTemplates.AsNoTracking().ToListAsync()) Assert.Equal(versions[p.Id], (p.Version, p.UpdatedAt));
            var builder = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!.GetMethod("BuildAsync")!;
            var exerciseById = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Id);
            foreach (var p in await db.ProgramTemplates.AsNoTracking().Where(p => !p.IsAssessment).ToListAsync())
            {
                ProgramWeeklyPatternRules.Validate(p.WeeklyPatternJson, false, true);
                var json = await (Task<string>)builder.Invoke(null, [db, p, null, CancellationToken.None])!;
                using var schedule = JsonDocument.Parse(json);
                Assert.Equal(182, schedule.RootElement.GetArrayLength());
                foreach (var slot in schedule.RootElement.EnumerateArray())
                {
                    var e = exerciseById[slot.GetProperty("ExerciseId").GetGuid()];
                    Assert.False(e.IsAssessmentOnly);
                    Assert.Equal(p.TargetAgeGroupConfigurationId, e.TargetAgeGroupId);
                    Assert.Equal(p.InitialDifficultyLevel, e.DifficultyLevel);
                }
            }
            Assert.Equal(15, await db.ReadingTexts.CountAsync());
            Assert.Equal(60, await db.ReadingQuestions.CountAsync());
            var sessionsType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var sessions = (ISpeedReadingExerciseSessions)Activator.CreateInstance(sessionsType, db)!;
            foreach (var f in fading.RootElement.EnumerateArray())
            {
                var started = await sessions.StartAsync(Guid.NewGuid(), new() { ExerciseId = f.GetProperty("id").GetGuid() });
                Assert.Equal(4, started.InitialData.GetProperty("questions").GetArrayLength());
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(session.ExerciseId, text.ExerciseId);
                Assert.Equal(f.GetProperty("level").GetInt32(), text.DifficultyLevel);
            }
            await Execute(await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql")));
            foreach (var p in await db.ProgramTemplates.AsNoTracking().ToListAsync())
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before[p.Id]), JsonNode.Parse(p.WeeklyPatternJson)));
            Assert.Equal(progressBefore, JsonSerializer.Serialize(await db.StudentProgramProgresses.AsNoTracking().SingleAsync()));
            Assert.Equal(5, await db.ExerciseSessions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static string Body(string sql) => Regex.Replace(sql, @"(?m)^(BEGIN;|COMMIT;)\r?$", "");

    [Theory]
    [InlineData("unstarted")]
    [InlineData("started")]
    [InlineData("open-session")]
    [InlineData("wrong-hash")]
    [InlineData("wrong-id")]
    [InlineData("orphan-slot")]
    public async Task Legacy_repair_only_changes_unstarted_known_scope_and_is_repeatable(string scenario)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var sql = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "content-packs/programs/v3/repair-unstarted-assessment-slots.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var actor = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
            var age = Guid.Parse("10000000-0000-0000-0000-000000000002");
            var normal = Exercise.Create("Normal", type.Name, "{}", 1, actor, type.Id, targetAgeGroupId: age);
            var measurement = Exercise.Create("Measure", type.Name, "{}", 1, actor, type.Id, targetAgeGroupId: age, isAssessmentOnly: true);
            db.ExerciseTypes.Add(type); db.Exercises.AddRange(normal, measurement);
            var template = ProgramTemplate.Import(Guid.Parse("e2c01002-8d9a-4e6c-a428-000000000001"), "Genç", "", age,
                0, 100, "{}", 1, 0, 1, 4, 28, true, 1, 0, null, false, DateTime.UtcNow, actor.ToString(), null, null);
            db.ProgramTemplates.Add(template);
            var progress = StudentProgramProgress.Start(Guid.NewGuid(), actor, template, 0, 0, actor, DateTime.UtcNow);
            progress.SetSchedule(JsonSerializer.Serialize(new[] { new { WeekNumber = 1, DayNumber = 1, Order = 1, ExerciseId = measurement.Id },
                new { WeekNumber = 1, DayNumber = 1, Order = 2, ExerciseId = scenario == "orphan-slot" ? Guid.NewGuid() : measurement.Id } }), actor, DateTime.UtcNow);
            if (scenario == "started") progress.ApplyExerciseCompletion(80, false, 1, 2, template, actor, DateTime.UtcNow);
            if (scenario == "open-session") db.ExerciseSessions.Add(ExerciseSession.Start(actor, measurement.Id, null, 1, DateTime.UtcNow, null));
            db.StudentProgramProgresses.Add(progress); await db.SaveChangesAsync();
            var before = (await db.StudentProgramProgresses.AsNoTracking().SingleAsync()).ScheduleJson;
            await using (var hashConnection = new NpgsqlConnection(postgres.ConnectionString))
            {
                await hashConnection.OpenAsync();
                await using var hashCommand = new NpgsqlCommand("SELECT md5(\"ScheduleJson\"::text) FROM speed_reading.student_program_progress WHERE id=@id", hashConnection);
                hashCommand.Parameters.AddWithValue("id", progress.Id);
                var hash = (string)(await hashCommand.ExecuteScalarAsync())!;
                sql = sql.Replace(":'repair_progress_id'", $"'{(scenario == "wrong-id" ? Guid.NewGuid() : progress.Id)}'")
                    .Replace(":'repair_schedule_md5'", $"'{(scenario == "wrong-hash" ? new string('0', 32) : hash)}'");
            }
            if (scenario != "unstarted")
            {
                await Assert.ThrowsAsync<PostgresException>(() => Execute(sql));
                Assert.Equal(before, (await db.StudentProgramProgresses.AsNoTracking().SingleAsync()).ScheduleJson);
            }
            else
            {
                await Execute(sql);
                var after = await db.StudentProgramProgresses.AsNoTracking().SingleAsync();
                using var schedule = JsonDocument.Parse(after.ScheduleJson!);
                Assert.All(schedule.RootElement.EnumerateArray(), slot => Assert.Equal(normal.Id, slot.GetProperty("ExerciseId").GetGuid()));
                Assert.Equal(new[] { 1, 2 }, schedule.RootElement.EnumerateArray().Select(x => x.GetProperty("Order").GetInt32()));
                await Execute(sql);
                var rerun = await db.StudentProgramProgresses.AsNoTracking().SingleAsync();
                Assert.Equal(after.ScheduleJson, rerun.ScheduleJson); Assert.Equal(after.Version, rerun.Version);
                Assert.Equal(0, rerun.ExercisesCompleted);
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> Inventory(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        do { while (await reader.ReadAsync()) if (reader.FieldCount == 1) result.Add(reader.GetString(0)); } while (await reader.NextResultAsync());
        return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_migration_marks_legacy_measurement_rows_or_atomically_rejects_source_drift(bool drift)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        using var catalog = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root!.FullName, "content-packs/assessment/v1/catalog.json")));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var actor = Guid.NewGuid();
            var types = new Dictionary<string, ExerciseType>();
            foreach (var row in catalog.RootElement.EnumerateArray())
            {
                var name = row.GetProperty("type").GetString()!;
                if (!types.ContainsKey(name)) { types[name] = ExerciseType.Create(Guid.NewGuid(), name, name, "focus"); db.ExerciseTypes.Add(types[name]); }
                db.Exercises.Add(Exercise.Import(row.GetProperty("id").GetGuid(), row.GetProperty("title").GetString()!, name,
                    row.GetProperty("configuration").GetRawText(), 1, actor, types[name].Id, DateTime.UtcNow,
                    row.GetProperty("targetAgeGroupId").GetGuid(), "", true,
                    drift ? "unexpected-source" : "system:assessment-v1", null, null));
            }
            await db.SaveChangesAsync();
            await Execute("ALTER TABLE speed_reading.exercises DROP COLUMN is_assessment_only;");
            var migration = new SpeedReading.Infrastructure.Persistence.Migrations.SeparateAssessmentExerciseScope();
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations, db.Model);
            var sql = "BEGIN;\n" + string.Join("\n", commands.Select(c => c.CommandText)) + "\nCOMMIT;";
            if (drift) await Assert.ThrowsAsync<PostgresException>(() => Execute(sql));
            else
            {
                await Execute(sql);
                Assert.Equal(12, await db.Exercises.AsNoTracking().CountAsync(e => e.IsAssessmentOnly));
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
