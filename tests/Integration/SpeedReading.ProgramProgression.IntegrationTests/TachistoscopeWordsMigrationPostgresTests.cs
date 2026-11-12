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
/// The November 2026 Tachistoscope word/phrase migration changes only content type, start length, start
/// duration and the length bounds of the 20 normal exercises, only when all of them still hold the reviewed
/// previous values, whatever their round count; Down restores them with the same guard.
/// </summary>
public sealed class TachistoscopeWordsMigrationPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Target = "20261112120000_TachistoscopeWordsAndShorterDurations";

    private static readonly (string Name, int MinAge, int? MaxAge, int MinDuration, int MaxDuration, int Interval)[] AgeGroups =
        [("Çocuk", 9, 12, 400, 2000, 750), ("Genç", 13, 16, 300, 1500, 500),
         ("Genç Yetişkin", 17, 21, 250, 1500, 500), ("Yetişkin", 22, null, 250, 1500, 500)];

    private static readonly (string Type, int Length, int Duration, int Min, int Max)[][] Old =
    [
        [("number", 3, 1500, 3, 5), ("number", 3, 1400, 3, 5), ("number", 4, 1300, 3, 5), ("number", 4, 1200, 3, 5), ("number", 5, 1100, 3, 5)],
        [("number", 3, 1000, 3, 6), ("number", 4, 900, 3, 6), ("number", 4, 800, 3, 6), ("number", 5, 700, 3, 6), ("number", 6, 600, 3, 6)],
        [("number", 4, 800, 4, 6), ("number", 4, 700, 4, 6), ("number", 5, 600, 4, 6), ("number", 5, 550, 4, 6), ("number", 6, 500, 4, 6)],
        [("number", 4, 800, 4, 7), ("number", 5, 700, 4, 7), ("number", 5, 600, 4, 7), ("number", 6, 550, 4, 7), ("number", 7, 500, 4, 7)]
    ];

    private static readonly (string Type, int Length, int Duration, int Min, int Max)[][] New =
    [
        [("number", 3, 1200, 3, 5), ("number", 3, 1120, 3, 5), ("word", 4, 1040, 3, 6), ("word", 5, 960, 4, 7), ("phrase", 8, 880, 6, 10)],
        [("number", 3, 800, 3, 6), ("number", 4, 720, 3, 6), ("word", 5, 640, 4, 7), ("word", 6, 560, 5, 8), ("phrase", 10, 480, 8, 12)],
        [("number", 4, 640, 4, 6), ("number", 4, 560, 4, 6), ("word", 6, 480, 4, 8), ("word", 7, 440, 5, 9), ("phrase", 11, 400, 9, 14)],
        [("number", 4, 640, 4, 7), ("number", 5, 560, 4, 7), ("word", 6, 480, 5, 9), ("word", 7, 440, 6, 10), ("phrase", 12, 400, 10, 16)]
    ];

    private static readonly int[][] Counts = [[20, 25, 30, 35, 40], [25, 30, 35, 40, 45], [30, 35, 40, 45, 50], [30, 35, 40, 45, 50]];

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, builder => builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
        .Options);

    private static string Previous(OwnedSpeedReadingDbContext db)
    {
        // The migration right before this one: 20261112100000 (round counts) once that is merged.
        var all = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).ToList();
        return all[all.IndexOf(Target) - 1];
    }

    private sealed record Seeded(Guid Id, int Age, int Level, bool Normal);

    [Fact]
    public async Task Up_changes_only_guarded_normal_settings_and_down_restores_them()
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
                // The round count must not matter: the Çocuk rows already carry the 20261112100000 values.
                var count = age == 0 ? 15 + level : Counts[age][level - 1];
                var exercise = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope",
                    Config(Old[age][level - 1], AgeGroups[age], count, level), level, actor, type.Id, targetAgeGroupId: ageIds[age]);
                db.Exercises.Add(exercise);
                seeded.Add(new(exercise.Id, age, level, true));
            }
            var assessment = Exercise.Create("Seviye Tespit", "Tachistoscope",
                $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":20,"length":{{{Old[age][0].Length}}}},"timing":{"durationMs":{{{Old[age][0].Duration}}},"intervalMs":500},"adaptive":{"enabled":false,"minLength":{{{Old[age][0].Min}}},"maxLength":{{{Old[age][0].Max}}}},"assessmentProtocol":{"version":2,"practiceRounds":3,"roundsPerBlock":6,"blockValues":[600,450,300]}},"difficultyLevel":1}""",
                1, actor, type.Id, targetAgeGroupId: ageIds[age], isAssessmentOnly: age % 2 == 0);
            db.Exercises.Add(assessment);
            seeded.Add(new(assessment.Id, age, 1, false));
        }
        var deleted = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope", Config(Old[1][2], AgeGroups[1], 35, 3), 3, actor,
            type.Id, targetAgeGroupId: ageIds[1]);
        deleted.Delete(actor, DateTime.UtcNow);
        db.Exercises.Add(deleted);
        await db.SaveChangesAsync();

        // Production may differ: an edited duration, a row without length bounds, a row with a root duration override.
        var editedDuration = seeded.Single(item => item is { Age: 3, Level: 2, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,timing,durationMs}', '650') WHERE id = @id", editedDuration);
        var withoutBounds = seeded.Single(item => item is { Age: 2, Level: 4, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = configuration_json #- '{engineConfig,adaptive,minLength}' #- '{engineConfig,adaptive,maxLength}' WHERE id = @id", withoutBounds);
        var overridden = seeded.Single(item => item is { Age: 1, Level: 5, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,displayDurationMs}', '600') WHERE id = @id", overridden);
        var skipped = new[] { editedDuration, withoutBounds, overridden };

        var before = await Snapshot();
        Assert.Equal(25, before.Count);

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
            var expected = With(before[item.Id].Config, New[item.Age][item.Level - 1]);
            Assert.True(JsonNode.DeepEquals(expected, config), $"{item.Age}/{item.Level}: {config.ToJsonString()}");
            Assert.Equal(before[item.Id].Version + 1, version);
            Assert.Equal("system:tachistoscope-words-2026-11", updatedBy);
            // Rounds, interval, duration bounds and adaptation stay as they were.
            foreach (var path in new[] { "count", "interval", "minDuration", "maxDuration", "enabled", "mode" })
                Assert.True(JsonNode.DeepEquals(Key(before[item.Id].Config, path), Key(config, path)), $"{item.Age}/{item.Level} {path}");
            // Every new start duration lies within the unchanged bounds.
            var duration = config["engineConfig"]!["timing"]!["durationMs"]!.GetValue<int>();
            Assert.InRange(duration, AgeGroups[item.Age].MinDuration, AgeGroups[item.Age].MaxDuration);
            var length = config["engineConfig"]!["content"]!["length"]!.GetValue<int>();
            Assert.InRange(length, config["engineConfig"]!["adaptive"]!["minLength"]!.GetValue<int>(),
                config["engineConfig"]!["adaptive"]!["maxLength"]!.GetValue<int>());
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.ToJsonString(), "text_stream");
        }
        Assert.True(JsonNode.DeepEquals(before[deleted.Id].Config, afterUp[deleted.Id].Config));

        // Up is idempotent: a second run changes nothing, not even the audit columns.
        await RunMigrationSql(db, up: true);
        var afterSecondUp = await Snapshot();
        foreach (var (id, row) in afterUp)
        {
            Assert.True(JsonNode.DeepEquals(row.Config, afterSecondUp[id].Config));
            Assert.Equal(row.Version, afterSecondUp[id].Version);
        }

        // An admin edit after the release is not overwritten by Down.
        var edited = seeded.Single(item => item is { Age: 0, Level: 5, Normal: true }).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,content,length}', '9') WHERE id = @id", edited);

        await db.Database.GetService<IMigrator>().MigrateAsync(previous);
        var afterDown = await Snapshot();
        foreach (var item in seeded)
        {
            var (config, version, updatedBy) = afterDown[item.Id];
            if (item.Id == edited)
            {
                Assert.Equal("phrase", config["engineConfig"]!["content"]!["type"]!.GetValue<string>());
                Assert.Equal(9, config["engineConfig"]!["content"]!["length"]!.GetValue<int>());
                continue;
            }
            Assert.True(JsonNode.DeepEquals(before[item.Id].Config, config), $"{item.Age}/{item.Level}: {config.ToJsonString()}");
            if (!item.Normal || skipped.Contains(item.Id))
            {
                Assert.Equal(before[item.Id].Version, version);
                continue;
            }
            Assert.Equal(before[item.Id].Version + 2, version);
            Assert.Equal("system:tachistoscope-words-2026-11-rollback", updatedBy);
        }

        // Down is idempotent too.
        await RunMigrationSql(db, up: false);
        var afterSecondDown = await Snapshot();
        foreach (var (id, row) in afterDown)
            Assert.Equal(row.Version, afterSecondDown[id].Version);
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
        await db.Database.GetService<IMigrator>().MigrateAsync(Previous(db));
        Assert.DoesNotContain(Target, await db.Database.GetAppliedMigrationsAsync());
    }

    private static string Config((string Type, int Length, int Duration, int Min, int Max) setting,
        (string Name, int MinAge, int? MaxAge, int MinDuration, int MaxDuration, int Interval) group, int count, int level) =>
        $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"{{{setting.Type}}}","count":{{{count}}},"length":{{{setting.Length}}}},"timing":{"durationMs":{{{setting.Duration}}},"intervalMs":{{{group.Interval}}}},"adaptive":{"enabled":true,"minLength":{{{setting.Min}}},"maxLength":{{{setting.Max}}},"minDurationMs":{{{group.MinDuration}}},"maxDurationMs":{{{group.MaxDuration}}}}},"difficultyLevel":{{{level}}}}""";

    private static JsonNode With(JsonNode original, (string Type, int Length, int Duration, int Min, int Max) setting)
    {
        var copy = original.DeepClone();
        copy["engineConfig"]!["content"]!["type"] = setting.Type;
        copy["engineConfig"]!["content"]!["length"] = setting.Length;
        copy["engineConfig"]!["timing"]!["durationMs"] = setting.Duration;
        copy["engineConfig"]!["adaptive"]!["minLength"] = setting.Min;
        copy["engineConfig"]!["adaptive"]!["maxLength"] = setting.Max;
        return copy;
    }

    private static JsonNode? Key(JsonNode config, string path) => path switch
    {
        "count" => config["engineConfig"]!["content"]!["count"],
        "interval" => config["engineConfig"]!["timing"]!["intervalMs"],
        "minDuration" => config["engineConfig"]!["adaptive"]!["minDurationMs"],
        "maxDuration" => config["engineConfig"]!["adaptive"]!["maxDurationMs"],
        "enabled" => config["engineConfig"]!["adaptive"]!["enabled"],
        _ => config["engineConfig"]!["mode"]
    };

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
