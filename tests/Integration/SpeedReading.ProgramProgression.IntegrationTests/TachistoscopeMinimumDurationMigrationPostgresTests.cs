using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

/// <summary>
/// Duration-focused Kısa Gösterim (26.11.2026): only <c>engineConfig.adaptive.minDurationMs</c> of the 20 normal
/// exercises changes (400/300/250/250 → 300/200/150/150 ms), only when the row still holds the reviewed November 2026
/// settings and the previous minimum; assessment, deleted and edited rows stay; Down restores the previous minimum.
/// </summary>
public sealed class TachistoscopeMinimumDurationMigrationPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Target = "20261126110000_TachistoscopeShorterMinimumDurations";

    private static readonly (string Name, int MinAge, int? MaxAge, int OldMinimum, int NewMinimum, int MaxDuration, int Interval)[] AgeGroups =
        [("Çocuk", 9, 12, 400, 300, 2000, 750), ("Genç", 13, 16, 300, 200, 1500, 500),
         ("Genç Yetişkin", 17, 21, 250, 150, 1500, 500), ("Yetişkin", 22, null, 250, 150, 1500, 500)];

    private static readonly (string Type, int Length, int Duration, int Min, int Max)[][] Settings =
    [
        [("number", 3, 1200, 3, 5), ("number", 3, 1120, 3, 5), ("word", 4, 1040, 3, 6), ("word", 5, 960, 4, 7), ("phrase", 8, 880, 6, 10)],
        [("number", 3, 800, 3, 6), ("number", 4, 720, 3, 6), ("word", 5, 640, 4, 7), ("word", 6, 560, 5, 8), ("phrase", 10, 480, 8, 12)],
        [("number", 4, 640, 4, 6), ("number", 4, 560, 4, 6), ("word", 6, 480, 4, 8), ("word", 7, 440, 5, 9), ("phrase", 11, 400, 9, 14)],
        [("number", 4, 640, 4, 7), ("number", 5, 560, 4, 7), ("word", 6, 480, 5, 9), ("word", 7, 440, 6, 10), ("phrase", 12, 400, 10, 16)]
    ];

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, builder => builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
        .Options);

    private static string Previous(OwnedSpeedReadingDbContext db)
    {
        var all = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).ToList();
        return all[all.IndexOf(Target) - 1];
    }

    private sealed record Seeded(Guid Id, int Age, int Level, bool Normal);

    [Fact]
    public async Task Up_lowers_only_guarded_normal_minimums_and_down_restores_them()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        var previous = Previous(db);
        await db.Database.GetService<IMigrator>().MigrateAsync(previous);

        var actor = Guid.NewGuid();
        var ageIds = new Guid[AgeGroups.Length];
        for (var index = 0; index < AgeGroups.Length; index++)
        {
            var group = AgeGroups[index];
            ageIds[index] = Guid.NewGuid();
            db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(ageIds[index], group.Name, group.Name, group.MinAge, group.MaxAge,
                100, 150, 300, 70, 20, 1, index + 1, true, null, actor, DateTime.UtcNow));
        }
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        db.ExerciseTypes.Add(type);

        var seeded = new List<Seeded>();
        for (var age = 0; age < AgeGroups.Length; age++)
        {
            for (var level = 1; level <= 5; level++)
            {
                var exercise = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope",
                    Config(Settings[age][level - 1], AgeGroups[age], AgeGroups[age].OldMinimum, level), level, actor, type.Id,
                    targetAgeGroupId: ageIds[age]);
                db.Exercises.Add(exercise);
                seeded.Add(new(exercise.Id, age, level, true));
            }
            var assessment = Exercise.Create("Seviye Tespit", "Tachistoscope",
                Config(Settings[age][0], AgeGroups[age], AgeGroups[age].OldMinimum, 1), 1, actor, type.Id,
                targetAgeGroupId: ageIds[age], isAssessmentOnly: true);
            db.Exercises.Add(assessment);
            seeded.Add(new(assessment.Id, age, 1, false));
        }
        await db.SaveChangesAsync();

        // Production may differ: an edited start duration, an edited minimum, a minimum spelled differently.
        var editedDuration = seeded.Single(item => item is { Age: 3, Level: 2, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,timing,durationMs}', '650') WHERE id = @id", editedDuration);
        var editedMinimum = seeded.Single(item => item is { Age: 1, Level: 4, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,adaptive,minDurationMs}', '350') WHERE id = @id", editedMinimum);
        var variant = seeded.Single(item => item is { Age: 2, Level: 5, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,adaptive,MinDurationMs}', '400') WHERE id = @id", variant);
        var skipped = new[] { editedDuration, editedMinimum, variant };

        var before = await Snapshot();
        await db.Database.GetService<IMigrator>().MigrateAsync(Target);
        var afterUp = await Snapshot();
        foreach (var item in seeded)
        {
            var (config, version, updatedBy) = afterUp[item.Id];
            if (!item.Normal || skipped.Contains(item.Id))
            {
                Assert.True(JsonNode.DeepEquals(before[item.Id].Config, config), $"{item.Age}/{item.Level} must not change");
                Assert.Equal(before[item.Id].Version, version);
                continue;
            }
            var expected = before[item.Id].Config.DeepClone();
            expected["engineConfig"]!["adaptive"]!["minDurationMs"] = AgeGroups[item.Age].NewMinimum;
            Assert.True(JsonNode.DeepEquals(expected, config), $"{item.Age}/{item.Level}: {config.ToJsonString()}");
            Assert.Equal(before[item.Id].Version + 1, version);
            Assert.Equal("system:tachistoscope-min-duration-2026-11", updatedBy);
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.ToJsonString(), "text_stream");
        }

        // Up is idempotent.
        await RunMigrationSql(db, up: true);
        var afterSecondUp = await Snapshot();
        foreach (var (id, row) in afterUp) Assert.Equal(row.Version, afterSecondUp[id].Version);

        await db.Database.GetService<IMigrator>().MigrateAsync(previous);
        var afterDown = await Snapshot();
        foreach (var item in seeded)
        {
            Assert.True(JsonNode.DeepEquals(before[item.Id].Config, afterDown[item.Id].Config), $"{item.Age}/{item.Level} after down");
            if (item.Normal && !skipped.Contains(item.Id))
                Assert.Equal("system:tachistoscope-min-duration-2026-11-rollback", afterDown[item.Id].UpdatedBy);
        }
    }

    [Fact]
    public async Task Migration_is_a_no_op_on_a_database_without_the_catalog()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        Assert.Contains(Target, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await db.Exercises.CountAsync());
        await RunMigrationSql(db, up: true);
        await RunMigrationSql(db, up: false);
    }

    private static string Config((string Type, int Length, int Duration, int Min, int Max) setting,
        (string Name, int MinAge, int? MaxAge, int OldMinimum, int NewMinimum, int MaxDuration, int Interval) group, int minimum, int level) =>
        $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"{{{setting.Type}}}","count":20,"length":{{{setting.Length}}}},"timing":{"durationMs":{{{setting.Duration}}},"intervalMs":{{{group.Interval}}}},"adaptive":{"enabled":true,"minLength":{{{setting.Min}}},"maxLength":{{{setting.Max}}},"minDurationMs":{{{minimum}}},"maxDurationMs":{{{group.MaxDuration}}}}},"difficultyLevel":{{{level}}}}""";

    private async Task<Dictionary<Guid, (JsonNode Config, int Version, string? UpdatedBy)>> Snapshot()
    {
        var rows = new Dictionary<Guid, (JsonNode, int, string?)>();
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, configuration_json::text, version, updated_by FROM speed_reading.exercises", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows[reader.GetGuid(0)] = (JsonNode.Parse(reader.GetString(1))!, reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3));
        return rows;
    }

    private async Task Execute(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RunMigrationSql(OwnedSpeedReadingDbContext db, bool up)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[Target], db.Database.ProviderName!);
        var operations = (up ? migration.UpOperations : migration.DownOperations).OfType<SqlOperation>().ToList();
        Assert.NotEmpty(operations);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        foreach (var operation in operations)
        {
            await using var command = new NpgsqlCommand(operation.Sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
