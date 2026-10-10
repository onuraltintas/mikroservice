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
public sealed class TeenProgramReplacementPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid Age = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly string[] OldIds =
    [
        "8d3f8827-c0ff-5b91-99cf-d01a192b9bbb", "4aa056b5-111f-5832-ac16-0f12466d00e6",
        "79c34e28-4e0d-538e-8b8f-a29d79906af7", "eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2",
        "7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15"
    ];

    [Theory]
    [InlineData("normal")]
    [InlineData("history")]
    [InlineData("collision")]
    [InlineData("missing-exercise")]
    [InlineData("invalid-version")]
    [InlineData("invalid-age")]
    [InlineData("invalid-shape")]
    [InlineData("duplicate-level")]
    [InlineData("invalid-type")]
    [InlineData("wrong-source-age")]
    public async Task Replacement_is_atomic_preserves_unrelated_data_and_resolves_all_five_schedules(string mode)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var root = FindRoot();
            var planJson = await File.ReadAllTextAsync(Path.Combine(root, "infrastructure/data/teen-program-plan-v2.json"));
            using var pack = JsonDocument.Parse(planJson);
            var actor = Guid.NewGuid();
            var types = pack.RootElement.GetProperty("programs").EnumerateArray()
                .SelectMany(program => program.GetProperty("days").EnumerateArray())
                .SelectMany(day => day.EnumerateArray()).Select(task => task.GetString()!).Distinct().ToList();
            foreach (var name in types)
            {
                var type = ExerciseType.Create(Guid.NewGuid(), name, name, "focus");
                db.ExerciseTypes.Add(type);
                for (var level = 1; level <= 5; level++)
                {
                    if (!HasLevel(name, level) || (mode == "missing-exercise" && name == "Comprehension" && level == 5)) continue;
                    db.Exercises.Add(Exercise.Create(name, name, "{}", level, actor, type.Id, targetAgeGroupId: Age));
                }
            }
            var oldTemplates = OldIds.Select((id, index) => Import(Guid.Parse(id), $"Old teen {index + 1}",
                mode == "wrong-source-age" && index == 0 ? Guid.NewGuid() : Age, index + 1)).ToList();
            db.ProgramTemplates.AddRange(oldTemplates);
            var assessment = Import(Guid.NewGuid(), "Preserved teen assessment", Age, 1, true);
            var child = Import(Guid.NewGuid(), "Preserved child program", Guid.NewGuid(), 1);
            db.ProgramTemplates.AddRange(assessment, child);
            var unrelatedProgress = StudentProgramProgress.Start(Guid.NewGuid(), actor, child, 40, 2, actor, DateTime.UtcNow);
            db.StudentProgramProgresses.Add(unrelatedProgress);
            if (mode == "history")
                db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), oldTemplates[0], 0, 0, actor, DateTime.UtcNow));
            if (mode == "collision")
                db.ProgramTemplates.Add(Import(Guid.Parse("e2c01002-8d9a-4e6c-a428-000000000001"), "Reserved id collision", Age, 1));
            var invalid = JsonNode.Parse(planJson)!.AsObject();
            switch (mode)
            {
                case "invalid-version": invalid["version"] = 3; break;
                case "invalid-age": invalid["ageGroupId"] = Guid.NewGuid().ToString(); break;
                case "invalid-shape": invalid["programs"]![0]!["days"]![0]!.AsArray().RemoveAt(0); break;
                case "duplicate-level": invalid["programs"]![1]!["level"] = 1; break;
                case "invalid-type": invalid["programs"]![0]!["days"]![0]![0] = "UnknownExercise"; break;
            }
            planJson = invalid.ToJsonString();
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var catalogBefore = JsonSerializer.Serialize(await db.Exercises.OrderBy(item => item.Id).ToListAsync());
            var assessmentBefore = JsonSerializer.Serialize(await db.ProgramTemplates.SingleAsync(item => item.Id == assessment.Id));
            var childBefore = JsonSerializer.Serialize(await db.ProgramTemplates.SingleAsync(item => item.Id == child.Id));
            var historyBefore = JsonSerializer.Serialize(await db.StudentProgramProgresses.SingleAsync(item => item.Id == unrelatedProgress.Id));
            var script = (await File.ReadAllTextAsync(Path.Combine(root, "content-packs/teen-programs/v2/apply.sql")))
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
                db.ChangeTracker.Clear();
                Assert.Equal(mode == "collision" ? 8 : 7, await db.ProgramTemplates.CountAsync());
                Assert.Equal(mode == "history" ? 2 : 1, await db.StudentProgramProgresses.CountAsync());
                Assert.Equal(5, await db.ProgramTemplates.CountAsync(item => OldIds.Select(Guid.Parse).Contains(item.Id)));
            }
            else
            {
                await Apply();
                await Apply(); // Re-running a verified pack must not duplicate templates.
                db.ChangeTracker.Clear();
                var templates = await db.ProgramTemplates.Where(item => item.TargetAgeGroupConfigurationId == Age && !item.IsAssessment).ToListAsync();
                Assert.Equal(5, templates.Count);
                Assert.False(await db.ProgramTemplates.AnyAsync(item => OldIds.Select(Guid.Parse).Contains(item.Id)));
                var builder = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                    "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!.GetMethod("BuildAsync", BindingFlags.Public | BindingFlags.Static)!;
                var catalogue = await db.Exercises.ToDictionaryAsync(item => item.Id);
                var typeNames = await db.ExerciseTypes.ToDictionaryAsync(item => item.Id, item => item.Name);
                foreach (var template in templates)
                {
                    Assert.Equal(0, template.WeeksPerDifficultyIncrease);
                    Assert.Equal(28, template.TotalDays);
                    Assert.Equal(Math.Min(template.InitialDifficultyLevel + 1, 5), template.MaxDifficultyLevel);
                    ProgramWeeklyPatternRules.Validate(template.WeeklyPatternJson, false, true);
                    var json = await (Task<string>)builder.Invoke(null, [db, template, null, CancellationToken.None])!;
                    using var schedule = JsonDocument.Parse(json);
                    var slots = schedule.RootElement.EnumerateArray().ToList();
                    Assert.Equal(182, slots.Count);
                    var days = slots.GroupBy(slot => (Week: slot.GetProperty("WeekNumber").GetInt32(), Day: slot.GetProperty("DayNumber").GetInt32())).ToList();
                    Assert.Equal(28, days.Count);
                    var approved = pack.RootElement.GetProperty("programs").EnumerateArray().Single(item => item.GetProperty("level").GetInt32() == template.InitialDifficultyLevel);
                    foreach (var day in days)
                    {
                        Assert.Equal(day.Key.Week <= 2 ? 6 : 7, day.Count());
                        Assert.Equal(Enumerable.Range(1, day.Count()), day.Select(slot => slot.GetProperty("Order").GetInt32()));
                        var expectedTypes = approved.GetProperty("days")[(day.Key.Week - 1) * 7 + day.Key.Day - 1].EnumerateArray().Select(item => item.GetString());
                        var actualExercises = day.Select(slot => catalogue[slot.GetProperty("ExerciseId").GetGuid()]).ToList();
                        Assert.Equal(expectedTypes, actualExercises.Select(exercise => typeNames[exercise.ExerciseTypeId]));
                        foreach (var exercise in actualExercises)
                        {
                            Assert.Equal(Age, exercise.TargetAgeGroupId);
                            Assert.Equal(typeNames[exercise.ExerciseTypeId] == "Chunking" ? Math.Min(template.InitialDifficultyLevel, 4) : template.InitialDifficultyLevel, exercise.DifficultyLevel);
                        }
                    }
                }
            }
            Assert.Equal(catalogBefore, JsonSerializer.Serialize(await db.Exercises.AsNoTracking().OrderBy(item => item.Id).ToListAsync()));
            Assert.Equal(assessmentBefore, JsonSerializer.Serialize(await db.ProgramTemplates.AsNoTracking().SingleAsync(item => item.Id == assessment.Id)));
            Assert.Equal(childBefore, JsonSerializer.Serialize(await db.ProgramTemplates.AsNoTracking().SingleAsync(item => item.Id == child.Id)));
            Assert.Equal(historyBefore, JsonSerializer.Serialize(await db.StudentProgramProgresses.AsNoTracking().SingleAsync(item => item.Id == unrelatedProgress.Id)));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static bool HasLevel(string name, int level) => name switch
    {
        "Chunking" => level <= 4,
        "FreeReading" => level <= 3,
        "RSVP" or "TextFading" or "Skimming" => level >= 3,
        "ErrorAnalysis" => level >= 4,
        "ExamSimulation" => level == 5,
        _ => true
    };

    private static ProgramTemplate Import(Guid id, string name, Guid age, int level, bool assessment = false) =>
        ProgramTemplate.Import(id, name, "Preserved source description", age, 0, 100, "{}", level, 2,
            Math.Min(level + 1, 5), assessment ? 1 : 4, assessment ? 1 : 28, true, level, 0, null, assessment,
            DateTime.UtcNow, "test", null, null);

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "infrastructure/data/teen-program-plan-v2.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository teen pack not found.");
    }
}
