using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

/// <summary>
/// The November 2026 data migration sets the approved round counts of the 40 normal Tachistoscope /
/// VisualExpansion exercises and the start/target distances of the 20 normal VisualExpansion exercises.
/// Rounds and distances are guarded independently: each changes only while every location of it still
/// holds the reviewed old value. Down restores the old values with the same guard.
/// </summary>
public sealed class RoundCountMigrationPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Previous = "20261029170000_ReadingSessionRejected";
    private const string Target = "20261112100000_UpdateRoundCountsAndVisualExpansionDistances";
    private const string Actor = "system:rounds-distances-2026-11";

    private static readonly (string Name, int MinAge, int? MaxAge)[] AgeGroups =
        [("Çocuk", 9, 12), ("Genç", 13, 16), ("Genç Yetişkin", 17, 21), ("Yetişkin", 22, null)];

    private static readonly int[][] OldTachistoscope =
        [[20, 25, 30, 35, 40], [25, 30, 35, 40, 45], [30, 35, 40, 45, 50], [30, 35, 40, 45, 50]];
    private static readonly int[][] OldVisualExpansion =
        [[30, 35, 40, 45, 50], [35, 40, 45, 50, 55], [40, 45, 50, 55, 60], [45, 50, 55, 60, 65]];
    private static readonly int[][] NewRounds =
        [[15, 19, 23, 26, 30], [20, 24, 28, 31, 35], [20, 24, 28, 31, 35], [20, 25, 30, 35, 40]];
    private const int OldStart = 8;
    private static readonly int[][] OldTarget =
        [[16, 18, 20, 22, 24], [18, 20, 22, 24, 26], [18, 20, 22, 24, 26], [18, 20, 22, 24, 26]];
    private static readonly int[][] NewStart =
        [[11, 14, 17, 20, 23], [11, 15, 19, 23, 27], [11, 15, 19, 23, 27], [11, 15, 19, 23, 27]];
    private static readonly int[][] NewTarget =
        [[21, 27, 33, 38, 44], [25, 32, 39, 45, 52], [25, 32, 39, 45, 52], [25, 33, 40, 48, 55]];

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, builder => builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
        .Options);

    private sealed record Seeded(Guid Id, string Type, int Age, int Level, bool Normal);

    [Fact]
    public async Task Up_changes_only_guarded_rounds_and_distances_and_down_restores_them()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        await db.Database.GetService<IMigrator>().MigrateAsync(Previous);

        var actor = Guid.NewGuid();
        var ageIds = new Guid[AgeGroups.Length];
        for (var index = 0; index < AgeGroups.Length; index++)
        {
            var (name, minAge, maxAge) = AgeGroups[index];
            ageIds[index] = Guid.NewGuid(); // production ids are not seeded in code: the migration must not need them
            db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(ageIds[index], name, name, minAge, maxAge,
                100, 150, 300, 70, 20, 1, index + 1, true, null, actor, DateTime.UtcNow));
        }
        var tachistoscope = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        var visual = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        db.ExerciseTypes.AddRange(tachistoscope, visual);

        var seeded = new List<Seeded>();
        for (var age = 0; age < AgeGroups.Length; age++)
        {
            for (var level = 1; level <= 5; level++)
            {
                var t = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope",
                    TachistoscopeConfig(OldTachistoscope[age][level - 1], level), level, actor, tachistoscope.Id,
                    targetAgeGroupId: ageIds[age]);
                var v = Exercise.Create("Çevredeki Harfler", "VisualExpansion",
                    VisualConfig(age, OldVisualExpansion[age][level - 1], OldStart, OldTarget[age][level - 1], level),
                    level, actor, visual.Id, targetAgeGroupId: ageIds[age]);
                db.Exercises.AddRange(t, v);
                seeded.Add(new(t.Id, "Tachistoscope", age, level, true));
                seeded.Add(new(v.Id, "VisualExpansion", age, level, true));
            }
            var tAssessment = Exercise.Create("Seviye Tespit", "Tachistoscope",
                $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":{{{OldTachistoscope[age][0]}}}},"assessmentProtocol":{"version":2,"practiceRounds":2,"roundsPerBlock":4}},"difficultyLevel":1}""",
                1, actor, tachistoscope.Id, targetAgeGroupId: ageIds[age], isAssessmentOnly: true);
            // Two VisualExpansion assessments are recognised only by their protocol, two only by the flag.
            var vAssessment = Exercise.Create("Seviye Tespit", "VisualExpansion", age % 2 == 0
                    ? $$$"""{"rounds":{{{OldVisualExpansion[age][0]}}},"startDegrees":8,"targetDegrees":{{{OldTarget[age][0]}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","assessmentProtocol":{"version":2}},"difficultyLevel":1}"""
                    : VisualConfig(age, OldVisualExpansion[age][0], OldStart, OldTarget[age][0], 1),
                1, actor, visual.Id, targetAgeGroupId: ageIds[age], isAssessmentOnly: age % 2 == 1);
            db.Exercises.AddRange(tAssessment, vAssessment);
            seeded.Add(new(tAssessment.Id, "Tachistoscope", age, 1, false));
            seeded.Add(new(vAssessment.Id, "VisualExpansion", age, 1, false));
        }
        // Extra shapes and production surprises, all at Genç (age 1). Each entry: config, whether rounds change,
        // whether distances change, and whether the server reads the distances from where a session can show them.
        var skipRounds = new HashSet<Guid>();
        var skipDistances = new HashSet<Guid>();
        var noSessionDistances = new HashSet<Guid>();
        void Extra(string type, int level, string config, bool rounds, bool distances, bool sessionDistances = true)
        {
            var exercise = Exercise.Create("Ek", type, config, level, actor, type == "Tachistoscope" ? tachistoscope.Id : visual.Id,
                targetAgeGroupId: ageIds[1]);
            db.Exercises.Add(exercise);
            seeded.Add(new(exercise.Id, type, 1, level, true));
            if (!rounds) skipRounds.Add(exercise.Id);
            if (!distances) skipDistances.Add(exercise.Id);
            if (!sessionDistances) noSessionDistances.Add(exercise.Id);
        }
        string R(int level) => OldVisualExpansion[1][level - 1].ToString(System.Globalization.CultureInfo.InvariantCulture);
        string T(int level) => OldTarget[1][level - 1].ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Root expansion only: the server ignores it while engineConfig is an object, but it is still a location to update.
        Extra("VisualExpansion", 1, $$$"""{"rounds":{{{R(1)}}},"expansion":{"startDegrees":8,"targetDegrees":{{{T(1)}}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal"},"difficultyLevel":1}""", true, true, sessionDistances: false);
        // engineConfig.expansion only, and both expansion objects.
        Extra("VisualExpansion", 2, $$$"""{"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","rounds":{{{R(2)}}},"expansion":{"startDegrees":8,"targetDegrees":{{{T(2)}}}}},"difficultyLevel":2}""", true, true);
        Extra("VisualExpansion", 3, $$$"""{"rounds":{{{R(3)}}},"expansion":{"startDegrees":8,"targetDegrees":{{{T(3)}}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","expansion":{"startDegrees":8,"targetDegrees":{{{T(3)}}}}},"difficultyLevel":3}""", true, true);
        // engineConfig.expansion still old while engineConfig already holds the new values: distances skipped.
        Extra("VisualExpansion", 4, $$$"""{"rounds":{{{R(4)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","startDegrees":{{{NewStart[1][3]}}},"targetDegrees":{{{NewTarget[1][3]}}},"expansion":{"startDegrees":8,"targetDegrees":{{{T(4)}}}}},"difficultyLevel":4}""", true, false);
        // Conflicting rounds (root old, engineConfig 99): rounds skipped, distances change.
        Extra("VisualExpansion", 5, $$$"""{"rounds":{{{R(5)}}},"startDegrees":8,"targetDegrees":{{{T(5)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","rounds":99},"difficultyLevel":5}""", false, true);
        // Round overrides, exact and in another case: rounds skipped, distances change.
        Extra("VisualExpansion", 1, $$$"""{"rounds":{{{R(1)}}},"totalSteps":12,"startDegrees":8,"targetDegrees":{{{T(1)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal"},"difficultyLevel":1}""", false, true);
        Extra("VisualExpansion", 2, $$$"""{"rounds":{{{R(2)}}},"startDegrees":8,"targetDegrees":{{{T(2)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","TotalSteps":12},"difficultyLevel":2}""", false, true);
        Extra("Tachistoscope", 1, $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","totalStimuli":12,"content":{"type":"number","count":{{{OldTachistoscope[1][0]}}}}},"difficultyLevel":1}""", false, false);
        Extra("Tachistoscope", 2, $$$"""{"TotalStimuli":12,"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":{{{OldTachistoscope[1][1]}}}}},"difficultyLevel":2}""", false, false);
        Extra("Tachistoscope", 3, $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","Count":12,"count":{{{OldTachistoscope[1][2]}}}}},"difficultyLevel":3}""", false, false);
        // The runtime reads keys case-insensitively: a key in another case skips the setting it affects.
        Extra("VisualExpansion", 3, $$$"""{"rounds":{{{R(3)}}},"startDegrees":8,"targetDegrees":{{{T(3)}}},"engineType":"visual_expansion","EngineConfig":{"expansion":{"startDegrees":8,"targetDegrees":{{{T(3)}}}}},"difficultyLevel":3}""", false, false);
        Extra("VisualExpansion", 4, $$$"""{"rounds":{{{R(4)}}},"startDegrees":8,"targetDegrees":{{{T(4)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","Expansion":{"startDegrees":8,"targetDegrees":{{{T(4)}}}}},"difficultyLevel":4}""", true, false);
        Extra("VisualExpansion", 5, $$$"""{"rounds":{{{R(5)}}},"StartDegrees":8,"startDegrees":8,"targetDegrees":{{{T(5)}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal"},"difficultyLevel":5}""", true, false);

        var deleted = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope", TachistoscopeConfig(25, 1), 1, actor,
            tachistoscope.Id, targetAgeGroupId: ageIds[1]);
        deleted.Delete(actor, DateTime.UtcNow);
        db.Exercises.Add(deleted);
        await db.SaveChangesAsync();

        Guid Normal(string type, int age, int level) =>
            seeded.First(item => item.Normal && item.Type == type && item.Age == age && item.Level == level).Id;
        // Production may differ from the report. Each case keeps the other (independent) change.
        var unexpectedRounds = Normal("VisualExpansion", 3, 2);   // rounds 52: distances still change
        var unexpectedStart = Normal("VisualExpansion", 1, 3);    // start 9: rounds still change
        var unexpectedAlias = Normal("VisualExpansion", 2, 4);    // an alias the player would read: rounds still change
        var conflictingTarget = Normal("VisualExpansion", 3, 4);  // root and engineConfig targets differ
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{rounds}', '52') WHERE id = @id", unexpectedRounds);
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{startDegrees}', '9') WHERE id = @id", unexpectedStart);
        await Execute("UPDATE speed_reading.exercises SET configuration_json = configuration_json || '{\"visualExpansionStartDegrees\":8}' WHERE id = @id", unexpectedAlias);
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,targetDegrees}', '25') WHERE id = @id", conflictingTarget);
        skipRounds.Add(unexpectedRounds);
        skipDistances.UnionWith([unexpectedStart, unexpectedAlias, conflictingTarget]);
        bool RoundsChange(Seeded item) => item.Normal && !skipRounds.Contains(item.Id);
        bool DistancesChange(Seeded item) => item.Normal && item.Type == "VisualExpansion" && !skipDistances.Contains(item.Id);

        var before = await Snapshot();
        Assert.Equal(seeded.Count + 1, before.Count);

        await db.Database.GetService<IMigrator>().MigrateAsync(Target);
        var afterUp = await Snapshot();
        foreach (var item in seeded)
        {
            var (config, version, updatedBy) = afterUp[item.Id];
            var expected = Expected(before[item.Id].Config, item,
                RoundsChange(item) ? NewRounds[item.Age][item.Level - 1] : null,
                DistancesChange(item) ? (NewStart[item.Age][item.Level - 1], NewTarget[item.Age][item.Level - 1]) : null);
            Assert.True(JsonNode.DeepEquals(expected, config), $"{item.Type} {item.Age}/{item.Level}: {config.ToJsonString()}");
            if (!RoundsChange(item) && !DistancesChange(item))
            {
                Assert.Equal(before[item.Id].Version, version);
                continue;
            }
            Assert.Equal(before[item.Id].Version + 1, version); // once per row, even when both change
            Assert.Equal(Actor, updatedBy);
        }
        Assert.True(JsonNode.DeepEquals(before[deleted.Id].Config, afterUp[deleted.Id].Config));

        // New sessions use the new count and distances.
        db.ChangeTracker.Clear();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var student = Guid.NewGuid();
        var startedIds = new Dictionary<Guid, Guid>();
        foreach (var item in seeded.Where(item => item.Normal))
        {
            var started = await service.StartAsync(student, new() { ExerciseId = item.Id });
            startedIds[item.Id] = started.SessionId;
            if (RoundsChange(item)) Assert.Equal(NewRounds[item.Age][item.Level - 1], started.TotalSteps);
            if (!DistancesChange(item) || noSessionDistances.Contains(item.Id)) continue;
            Assert.Equal(NewStart[item.Age][item.Level - 1], started.InitialData.GetProperty("visualExpansionStartDegrees").GetInt32());
            Assert.Equal(NewStart[item.Age][item.Level - 1], started.InitialData.GetProperty("visualExpansionCurrentDegrees").GetInt32());
            Assert.Equal(NewTarget[item.Age][item.Level - 1], started.InitialData.GetProperty("visualExpansionTargetDegrees").GetInt32());
        }

        // Up is idempotent: a second run changes nothing, not even the audit columns.
        await RunMigrationSql(db, up: true);
        var afterSecondUp = await Snapshot();
        foreach (var (id, row) in afterUp)
        {
            Assert.True(JsonNode.DeepEquals(row.Config, afterSecondUp[id].Config));
            Assert.Equal(row.Version, afterSecondUp[id].Version);
        }

        // Admin edits after the release are not overwritten by Down.
        var editedCount = Normal("Tachistoscope", 0, 1);
        var editedTarget = Normal("VisualExpansion", 0, 2);
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,content,count}', '99') WHERE id = @id", editedCount);
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,targetDegrees}', '30') WHERE id = @id", editedTarget);

        await db.Database.GetService<IMigrator>().MigrateAsync(Previous);
        var afterDown = await Snapshot();
        foreach (var item in seeded)
        {
            var (config, version, updatedBy) = afterDown[item.Id];
            if (item.Id == editedCount)
            {
                Assert.Equal(99, config["engineConfig"]!["content"]!["count"]!.GetValue<int>());
                continue;
            }
            var expected = item.Id == editedTarget
                ? Expected(before[item.Id].Config, item, null, (NewStart[0][1], 30))
                : before[item.Id].Config;
            Assert.True(JsonNode.DeepEquals(expected, config), $"{item.Type} {item.Age}/{item.Level}: {config.ToJsonString()}");
            if (!RoundsChange(item) && !DistancesChange(item))
            {
                Assert.Equal(before[item.Id].Version, version);
                continue;
            }
            Assert.Equal(before[item.Id].Version + 2, version);
            Assert.Equal(Actor + "-rollback", updatedBy);
        }

        // Down is idempotent too, and sessions that already started keep their own settings.
        await RunMigrationSql(db, up: false);
        var afterSecondDown = await Snapshot();
        foreach (var (id, row) in afterDown)
            Assert.Equal(row.Version, afterSecondDown[id].Version);
        db.ChangeTracker.Clear();
        foreach (var item in seeded.Where(item => RoundsChange(item)))
            Assert.Equal(NewRounds[item.Age][item.Level - 1],
                (await db.ExerciseSessions.AsNoTracking().SingleAsync(session => session.Id == startedIds[item.Id])).TotalSteps);
        foreach (var item in seeded.Where(item => DistancesChange(item) && !noSessionDistances.Contains(item.Id)))
        {
            var state = JsonNode.Parse((await db.ExerciseSessions.AsNoTracking()
                .SingleAsync(session => session.Id == startedIds[item.Id])).SessionDataJson)!;
            Assert.Equal(NewTarget[item.Age][item.Level - 1], state["visualExpansionTargetDegrees"]!.GetValue<int>());
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
        await db.Database.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.DoesNotContain(Target, await db.Database.GetAppliedMigrationsAsync());
    }

    private static string TachistoscopeConfig(int count, int level) =>
        $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":{{{count}}},"length":3},"timing":{"durationMs":1500,"intervalMs":750},"adaptive":{"enabled":true,"minLength":3,"maxLength":5,"minDurationMs":400,"maxDurationMs":2000}},"difficultyLevel":{{{level}}}}""";

    // Four shapes: the repository content pack (everything in engineConfig), the production report (everything
    // at the root), rounds at the root with distances in engineConfig, and distances in both places.
    private static string VisualConfig(int age, int rounds, int start, int target, int level) => age switch
    {
        0 => $$$"""{"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","rounds":{{{rounds}}},"displayDurationMs":1200,"startDegrees":{{{start}}},"targetDegrees":{{{target}}},"content":{"stimulusType":"letter"}},"difficultyLevel":{{{level}}}}""",
        1 => $$$"""{"rounds":{{{rounds}}},"startDegrees":{{{start}}},"targetDegrees":{{{target}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","displayDurationMs":1100,"content":{"stimulusType":"letter"}},"difficultyLevel":{{{level}}}}""",
        2 => $$$"""{"rounds":{{{rounds}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","displayDurationMs":1100,"startDegrees":{{{start}}},"targetDegrees":{{{target}}},"content":{"stimulusType":"letter"}},"difficultyLevel":{{{level}}}}""",
        _ => $$$"""{"rounds":{{{rounds}}},"startDegrees":{{{start}}},"targetDegrees":{{{target}}},"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","displayDurationMs":1100,"startDegrees":{{{start}}},"targetDegrees":{{{target}}},"content":{"stimulusType":"letter"}},"difficultyLevel":{{{level}}}}"""
    };

    // Applies a change to every exact (camelCase) location that is present.
    private static JsonNode Expected(JsonNode original, Seeded item, int? rounds, (int Start, int Target)? distances)
    {
        var copy = original.DeepClone();
        static JsonObject? Child(JsonNode? node, string name) => node is JsonObject obj && obj[name] is JsonObject child ? child : null;
        var root = copy.AsObject();
        var engine = Child(root, "engineConfig");
        if (rounds is { } count)
        {
            if (item.Type == "Tachistoscope") Child(engine, "content")!["count"] = count;
            else
                foreach (var holder in new[] { root, engine })
                    if (holder?.ContainsKey("rounds") == true) holder["rounds"] = count;
        }
        if (distances is { } d)
            foreach (var holder in new[] { root, Child(root, "expansion"), engine, Child(engine, "expansion") })
            {
                if (holder is null) continue;
                if (holder.ContainsKey("startDegrees")) holder["startDegrees"] = d.Start;
                if (holder.ContainsKey("targetDegrees")) holder["targetDegrees"] = d.Target;
            }
        return copy;
    }

    [Fact]
    public async Task Age_band_with_two_active_groups_of_the_same_minimum_age_is_skipped()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        await db.Database.GetService<IMigrator>().MigrateAsync(Previous);
        var actor = Guid.NewGuid();
        var first = Guid.NewGuid();
        db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(first, "Çocuk", "Çocuk", 9, 12, 100, 150, 300, 70, 20, 1, 1, true, null, actor, DateTime.UtcNow));
        db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(Guid.NewGuid(), "Çocuk 2", "Çocuk 2", 9, 11, 100, 150, 300, 70, 20, 1, 2, true, null, actor, DateTime.UtcNow));
        var tachistoscope = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        var visual = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        db.ExerciseTypes.AddRange(tachistoscope, visual);
        db.Exercises.AddRange(
            Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope", TachistoscopeConfig(20, 1), 1, actor, tachistoscope.Id, targetAgeGroupId: first),
            Exercise.Create("Çevredeki Harfler", "VisualExpansion", VisualConfig(0, 30, 8, 16, 1), 1, actor, visual.Id, targetAgeGroupId: first));
        await db.SaveChangesAsync();
        var before = await Snapshot();
        Assert.Equal(2, before.Count);

        await db.Database.GetService<IMigrator>().MigrateAsync(Target);
        var after = await Snapshot();
        foreach (var (id, row) in before)
        {
            Assert.True(JsonNode.DeepEquals(row.Config, after[id].Config));
            Assert.Equal(row.Version, after[id].Version);
        }
    }

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
