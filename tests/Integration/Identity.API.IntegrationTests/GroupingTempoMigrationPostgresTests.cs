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

namespace Identity.API.IntegrationTests;

/// <summary>
/// Kelime Gruplama tempo by age and level (26.11.2026): rows holding exactly the previous grouping pack configuration,
/// or before the pack exactly the previous catalog configuration, move to the regenerated pack configuration; edited,
/// unrelated and deleted rows stay; Down restores the exact source configuration; both directions are idempotent.
/// </summary>
[Collection("Database")]
public sealed class GroupingTempoMigrationPostgresTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    // The "Database" collection shares one database: leave it empty for the next test.
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }

    private const string Target = "20261126120000_GroupingTempoByAge";

    // id, age index, level, previous catalog configuration (levels 1–4), previous pack configuration, new configuration.
    private static readonly (string Id, int Age, int Level, string? Catalog, string Pack, string New)[] Rows =
    [
        ("c034a403-0215-51c1-8727-c6e690c5226f", 0, 1, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":100,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":150,"maxWordCount":200},"pacer":{"chunkSize":2,"speedWpm":100}}}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":160,"maxWordCount":460},"pacer":{"chunkSize":2,"speedWpm":100}}}"""),
        ("42296217-4ce6-5acd-88a3-fe636cbffb30", 0, 2, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":130,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":195,"maxWordCount":260},"pacer":{"chunkSize":3,"speedWpm":130}}}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":220,"maxWordCount":470},"pacer":{"chunkSize":2,"speedWpm":120}}}"""),
        ("3e0c4ca7-b16d-5700-8e28-79724f53dd94", 0, 3, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":160,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":3}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":240,"maxWordCount":320},"pacer":{"chunkSize":4,"speedWpm":160}}}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":260,"maxWordCount":400},"pacer":{"chunkSize":2,"speedWpm":140}}}"""),
        ("158e91aa-b6b4-50f9-afbc-752d59c9cd07", 0, 4, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":200,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":400},"pacer":{"chunkSize":4,"speedWpm":200}}}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":390},"pacer":{"chunkSize":3,"speedWpm":160}}}"""),
        ("d7b10000-0000-5000-8000-000000000001", 0, 5, null,
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":345,"maxWordCount":460},"pacer":{"chunkSize":4,"speedWpm":230}}}""",
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":400},"pacer":{"chunkSize":3,"speedWpm":180}}}"""),
        ("89bedb19-f023-5d7f-9a3f-9a986f9d33c2", 1, 1, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":150,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":375},"pacer":{"chunkSize":2,"speedWpm":150}}}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":280,"maxWordCount":430},"pacer":{"chunkSize":2,"speedWpm":130}}}"""),
        ("4d2d13e4-92b0-5201-b847-5c7f1bd0edbb", 1, 2, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":450},"pacer":{"chunkSize":3,"speedWpm":180}}}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":280,"maxWordCount":400},"pacer":{"chunkSize":3,"speedWpm":150}}}"""),
        ("bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f", 1, 3, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":525},"pacer":{"chunkSize":3,"speedWpm":210}}}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":490},"pacer":{"chunkSize":3,"speedWpm":180}}}"""),
        ("c93c2dec-eb3b-50b6-890d-4c416037565d", 1, 4, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":480,"maxWordCount":600},"pacer":{"chunkSize":4,"speedWpm":240}}}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":410,"maxWordCount":560},"pacer":{"chunkSize":3,"speedWpm":210}}}"""),
        ("d7b10000-0000-5000-8000-000000000002", 1, 5, null,
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":675},"pacer":{"chunkSize":4,"speedWpm":270}}}""",
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":640},"pacer":{"chunkSize":4,"speedWpm":240}}}"""),
        ("617a2e9d-3aed-5afb-b20b-39bcbc2848ba", 2, 1, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":540},"pacer":{"chunkSize":2,"speedWpm":180}}}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":450,"maxWordCount":770},"pacer":{"chunkSize":2,"speedWpm":180}}}"""),
        ("c53fae0b-d606-539d-b02a-60d080fc8e75", 2, 2, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":630},"pacer":{"chunkSize":3,"speedWpm":210}}}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":510,"maxWordCount":730},"pacer":{"chunkSize":3,"speedWpm":210}}}"""),
        ("eb87713b-d508-5d54-8556-fe7f0765bd90", 2, 3, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":480,"maxWordCount":720},"pacer":{"chunkSize":3,"speedWpm":240}}}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":580,"maxWordCount":730},"pacer":{"chunkSize":3,"speedWpm":240}}}"""),
        ("57d186e0-8756-5e69-aaa2-3dd457c931b9", 2, 4, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":270,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":810},"pacer":{"chunkSize":4,"speedWpm":270}}}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":690},"pacer":{"chunkSize":4,"speedWpm":270}}}"""),
        ("d7b10000-0000-5000-8000-000000000004", 2, 5, null,
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":600,"maxWordCount":900},"pacer":{"chunkSize":5,"speedWpm":300}}}""",
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":430,"maxWordCount":700},"pacer":{"chunkSize":4,"speedWpm":300}}}"""),
        ("02fc1e52-80d6-506e-9317-fd5eec33ea2d", 3, 1, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":200,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":400,"maxWordCount":600},"pacer":{"chunkSize":2,"speedWpm":200}}}""",
            """{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":590},"pacer":{"chunkSize":2,"speedWpm":200}}}"""),
        ("8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65", 3, 2, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":230,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":460,"maxWordCount":690},"pacer":{"chunkSize":3,"speedWpm":230}}}""",
            """{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":590},"pacer":{"chunkSize":3,"speedWpm":230}}}"""),
        ("1c5d756b-2bef-58e8-99c2-1e32d32e8153", 3, 3, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":260,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":520,"maxWordCount":780},"pacer":{"chunkSize":3,"speedWpm":260}}}""",
            """{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":340,"maxWordCount":820},"pacer":{"chunkSize":3,"speedWpm":260}}}"""),
        ("e4900566-c2ba-5254-bde7-215bb4ae5ca7", 3, 4, """{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":290,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":580,"maxWordCount":870},"pacer":{"chunkSize":4,"speedWpm":290}}}""",
            """{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":680,"maxWordCount":960},"pacer":{"chunkSize":4,"speedWpm":290}}}"""),
        ("d7b10000-0000-5000-8000-000000000003", 3, 5, null,
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":640,"maxWordCount":960},"pacer":{"chunkSize":5,"speedWpm":320}}}""",
            """{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":470,"maxWordCount":1050},"pacer":{"chunkSize":4,"speedWpm":320}}}""")
    ];

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, builder => builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
        .Options);

    private static string Previous(OwnedSpeedReadingDbContext db)
    {
        var all = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).ToList();
        return all[all.IndexOf(Target) - 1];
    }

    [Fact]
    public async Task Up_moves_pack_and_catalog_rows_to_the_new_settings_and_down_restores_each_source()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        var previous = Previous(db);
        await db.Database.GetService<IMigrator>().MigrateAsync(previous);

        var actor = Guid.NewGuid();
        var ages = new[] { (9, (int?)12), (13, 16), (17, 21), (22, null) };
        var ageIds = ages.Select(_ => Guid.NewGuid()).ToArray();
        for (var index = 0; index < ages.Length; index++)
            db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(ageIds[index], $"Yaş {index}", $"Yaş {index}", ages[index].Item1,
                ages[index].Item2, 100, 150, 300, 70, 20, 1, index + 1, true, null, actor, DateTime.UtcNow));
        var type = ExerciseType.Create(Guid.NewGuid(), "Chunking", "Gruplama", "word_highlight");
        db.ExerciseTypes.Add(type);
        // Children before the pack (catalog configuration where one exists), everyone else after it.
        var fromCatalog = Rows.Where(row => row.Age == 0 && row.Catalog is not null).Select(row => row.Id).ToHashSet();
        foreach (var row in Rows)
            db.Exercises.Add(Exercise.Create("Kelime Gruplarıyla Okuma", "reading",
                fromCatalog.Contains(row.Id) ? row.Catalog! : row.Pack, row.Level, actor, type.Id,
                id: Guid.Parse(row.Id), targetAgeGroupId: ageIds[row.Age]));
        var unrelated = Exercise.Create("Kelime Gruplarıyla Okuma", "reading", Rows[0].Pack, Rows[0].Level, actor, type.Id,
            targetAgeGroupId: ageIds[0]);
        db.Exercises.Add(unrelated);
        await db.SaveChangesAsync();
        // An administrator edit keeps the row out of the migration.
        var edited = Rows.Single(row => row.Age == 1 && row.Level == 3).Id;
        await Execute("UPDATE speed_reading.exercises SET configuration_json = jsonb_set(configuration_json, '{engineConfig,pacer,speedWpm}', '205') WHERE id = @id", Guid.Parse(edited));

        var before = await Snapshot();
        await db.Database.GetService<IMigrator>().MigrateAsync(Target);
        var afterUp = await Snapshot();
        foreach (var row in Rows)
        {
            var id = Guid.Parse(row.Id);
            if (row.Id == edited)
            {
                Assert.True(JsonNode.DeepEquals(before[id].Config, afterUp[id].Config));
                Assert.Equal(before[id].Version, afterUp[id].Version);
                continue;
            }
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.New), afterUp[id].Config), $"{row.Age}/{row.Level}: {afterUp[id].Config.ToJsonString()}");
            Assert.Equal(before[id].Version + 1, afterUp[id].Version);
            Assert.Equal(fromCatalog.Contains(row.Id) ? "system:grouping-tempo-2026-11-catalog" : "system:grouping-tempo-2026-11-pack", afterUp[id].UpdatedBy);
            ExerciseConfigurationRules.ValidateActiveConfiguration(afterUp[id].Config.ToJsonString(), "word_highlight");
        }
        Assert.True(JsonNode.DeepEquals(before[unrelated.Id].Config, afterUp[unrelated.Id].Config), "another exercise id never changes");

        await RunMigrationSql(db, up: true);
        foreach (var (id, row) in await Snapshot()) Assert.Equal(afterUp[id].Version, row.Version);

        await db.Database.GetService<IMigrator>().MigrateAsync(previous);
        var afterDown = await Snapshot();
        foreach (var row in Rows)
        {
            var id = Guid.Parse(row.Id);
            Assert.True(JsonNode.DeepEquals(before[id].Config, afterDown[id].Config), $"{row.Age}/{row.Level} after down");
        }
        await RunMigrationSql(db, up: false);
        foreach (var (id, row) in await Snapshot()) Assert.Equal(afterDown[id].Version, row.Version);
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

    [Fact]
    public void New_settings_are_exactly_the_regenerated_grouping_pack()
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "content-packs", "grouping", "v1", "settings.json")))!.AsArray();
        foreach (var row in Rows)
        {
            var pack = settings.Single(item => item!["id"]!.GetValue<string>() == row.Id)!;
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.New), pack["configuration"]), $"{row.Age}/{row.Level}");
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return directory!.FullName;
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
