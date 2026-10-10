using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class ChildProgramReplacementPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("normal")]
    [InlineData("history")]
    [InlineData("collision")]
    [InlineData("invalid-plan")]
    public async Task Replacement_preserves_catalog_assessment_and_history_and_builds_all_five_schedules(string mode)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var root = FindRoot();
            var planJson = await File.ReadAllTextAsync(Path.Combine(root, "infrastructure/data/child-program-plan-v2.json"));
            using var plan = JsonDocument.Parse(planJson);
            var age = Guid.Parse(plan.RootElement.GetProperty("ageGroupId").GetString()!);
            var taskTypes = plan.RootElement.GetProperty("days").EnumerateArray()
                .SelectMany(day => day.EnumerateArray().Select(task => task.GetString()!)).Distinct().ToList();
            var user = Guid.NewGuid();
            foreach (var typeName in taskTypes)
            {
                var type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, "focus");
                db.ExerciseTypes.Add(type);
                for (var level = 1; level <= 5; level++)
                {
                    if ((typeName == "Chunking" && level == 5) || (typeName == "FreeReading" && level == 4)) continue;
                    db.Exercises.Add(Exercise.Create(typeName, typeName, "{}", level, user, type.Id, targetAgeGroupId: age));
                }
            }
            using var oldPlans = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "content-packs/child-programs/v1/programs.json")));
            var oldTemplates = oldPlans.RootElement.EnumerateArray().Select(item => ProgramTemplate.Import(
                item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString()!, "old", age,
                0, 100, item.GetProperty("plan").GetRawText(), item.GetProperty("start").GetInt32(), 2,
                item.GetProperty("max").GetInt32(), 4, 28, true, 1, 0, null, false,
                DateTime.UtcNow, null, null, null)).ToList();
            db.ProgramTemplates.AddRange(oldTemplates);
            var assessment = ProgramTemplate.Import(Guid.NewGuid(), "Preserved assessment", "", age,
                0, 100, "{}", 1, 0, 5, 1, 1, true, 0, 0, null, true, DateTime.UtcNow, null, null, null);
            db.ProgramTemplates.Add(assessment);
            if (mode == "history")
                db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user,
                    oldTemplates[0], 0, 0, user, DateTime.UtcNow));
            if (mode == "collision")
            {
                var pattern = new Dictionary<string, object> { ["placementLevel"] = 1 };
                var days = plan.RootElement.GetProperty("days").EnumerateArray().ToList();
                for (var week = 1; week <= 4; week++)
                    pattern[$"week{week}"] = Enumerable.Range(1, 7).ToDictionary(day => $"day{day}",
                        day => days[(week - 1) * 7 + day - 1].EnumerateArray()
                            .Select(task => new { type = task.GetString(), count = 1, difficulty = 1 }).ToArray());
                db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.Parse("e2c01001-8d9a-4e6c-a428-000000000001"),
                    "Reserved id collision", "", age, 0, 100, JsonSerializer.Serialize(pattern),
                    1, 0, 2, 4, 28, true, 1, 0, null, true, DateTime.UtcNow, null, null, null));
            }
            if (mode == "invalid-plan")
            {
                var invalid = JsonNode.Parse(planJson)!.AsObject();
                invalid["version"] = 3;
                planJson = invalid.ToJsonString();
            }
            await db.SaveChangesAsync();
            var catalogCount = await db.Exercises.CountAsync();
            var script = (await File.ReadAllTextAsync(Path.Combine(root, "content-packs/child-programs/v2/apply.sql")))
                .Replace(":'plan_json'", "@plan_json", StringComparison.Ordinal);
            async Task Apply()
            {
                await using var connection = new NpgsqlConnection(postgres.ConnectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(script, connection);
                command.Parameters.Add(new NpgsqlParameter("plan_json", NpgsqlDbType.Jsonb) { Value = planJson });
                await command.ExecuteNonQueryAsync();
            }
            if (mode != "normal")
            {
                await Assert.ThrowsAsync<PostgresException>(Apply);
                Assert.Equal(mode == "collision" ? 7 : 6, await db.ProgramTemplates.CountAsync());
                Assert.Equal(mode == "history" ? 1 : 0, await db.StudentProgramProgresses.CountAsync());
                return;
            }
            await Apply();
            await Apply(); // Idempotent even on a reused connection.
            db.ChangeTracker.Clear();
            var templates = await db.ProgramTemplates.Where(item => !item.IsAssessment).ToListAsync();
            Assert.Equal(5, templates.Count);
            Assert.Equal(catalogCount, await db.Exercises.CountAsync());
            Assert.Equal("{}", (await db.ProgramTemplates.SingleAsync(item => item.Id == assessment.Id)).WeeklyPatternJson);
            Assert.False(await db.ProgramTemplates.AnyAsync(item => oldTemplates.Select(old => old.Id).Contains(item.Id)));
            var builder = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!.GetMethod("BuildAsync", BindingFlags.Public | BindingFlags.Static)!;
            foreach (var template in templates)
            {
                Assert.Equal(0, template.WeeksPerDifficultyIncrease);
                var scheduleJson = await (Task<string>)builder.Invoke(null, [db, template, null, CancellationToken.None])!;
                using var schedule = JsonDocument.Parse(scheduleJson);
                var slots = schedule.RootElement.EnumerateArray().ToList();
                Assert.Equal(182, slots.Count);
                var days = slots.GroupBy(slot => (Week: slot.GetProperty("WeekNumber").GetInt32(), Day: slot.GetProperty("DayNumber").GetInt32())).ToList();
                Assert.Equal(28, days.Count);
                foreach (var day in days)
                {
                    Assert.Equal(day.Key.Week <= 2 ? 6 : 7, day.Count());
                    Assert.Equal(day.Count(), day.Select(slot => slot.GetProperty("Order").GetInt32()).Distinct().Count());
                }
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "infrastructure/data/child-program-plan-v2.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository child plan not found.");
    }
}
