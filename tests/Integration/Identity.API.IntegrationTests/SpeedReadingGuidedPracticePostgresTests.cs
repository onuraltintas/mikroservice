using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingGuidedPracticePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Applies_pack_idempotently_and_verifies_twenty_paced_sessions_without_measured_WPM()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var (settings, sql, student) = await Seed(db);
            var oldTexts = await db.ReadingTexts.ToDictionaryAsync(t => t.Id, t => t.Content);
            await ExecutePack(db, sql);
            db.ChangeTracker.Clear();
            var versions = await db.Exercises.ToDictionaryAsync(e => e.Id, e => e.Version);
            await ExecutePack(db, sql);
            db.ChangeTracker.Clear();
            Assert.Equal(oldTexts.Count + 22, await db.ReadingTexts.CountAsync());
            foreach (var old in oldTexts) Assert.Equal(old.Value, (await db.ReadingTexts.SingleAsync(t => t.Id == old.Key)).Content);
            foreach (var exercise in await db.Exercises.ToListAsync()) Assert.Equal(versions[exercise.Id], exercise.Version);
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var item in settings)
            {
                var exerciseId = Guid.Parse(item!["id"]!.GetValue<string>());
                var started = await service.StartAsync(student, new() { ExerciseId = exerciseId });
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                var content = item["configuration"]!["engineConfig"]!["content"]!;
                var speed = item["configuration"]!["engineConfig"]!["pacer"]!["speedWpm"]!.GetValue<int>();
                Assert.InRange(text.WordCount, content["minWordCount"]!.GetValue<int>(), content["maxWordCount"]!.GetValue<int>());
                Assert.Equal(Guid.Parse(item["ageGroupId"]!.GetValue<string>()), text.TargetAgeGroupId);
                Assert.Equal(item["level"]!.GetValue<int>(), text.DifficultyLevel);
                Assert.Equal(1, started.InitialData.GetProperty("groupingChunkSize").GetInt32());
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), session.Id, new() { Action = "start_reading" }));
                Assert.True((await service.ValidateActionAsync(student, session.Id, new() { Action = "start_reading" })).IsValid);
                Assert.False((await service.ValidateActionAsync(student, session.Id, new() { Action = "finish_reading" })).IsValid);
                await service.PauseAsync(student, session.Id);
                await service.ResumeAsync(student, session.Id);
                var state = JsonNode.Parse(session.SessionDataJson)!;
                var minimum = state["readingMinimumMs"]!.GetValue<int>();
                Assert.Equal((int)Math.Ceiling(text.WordCount * 60000m / speed), minimum);
                state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(-minimum - 5100);
                state["readingPausedMilliseconds"] = 5000;
                state["readingPausedSeconds"] = 5;
                session.SetState(state.ToJsonString());
                await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(student, session.Id, new() { Action = "finish_reading" })).IsValid);
                var result = await service.CompleteAsync(student, session.Id, new());
                Assert.Null(result.RawWPM);
                Assert.Null(result.Accuracy);
                Assert.Null(result.ComprehensionScore);
                Assert.Equal(speed, result.DetailedResults.GetProperty("groupingDisplayPaceWpm").GetDecimal());
                Assert.Equal(100, result.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal());
                Assert.Equal(text.WordCount, result.DetailedResults.GetProperty("wordCount").GetInt32());
                Assert.Equal(0, result.XpGained);
                var repeated = await service.CompleteAsync(student, session.Id, new());
                Assert.Null(repeated.RawWPM);
            }
            Assert.Equal(20, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("settings")]
    public async Task Refuses_changed_sources_or_settings_without_partial_publication(string changed)
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var (_, sql, _) = await Seed(db);
            var textCount = await db.ReadingTexts.CountAsync();
            if (changed == "source")
                await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.reading_texts SET content=regexp_replace(content, 'kaynak', 'farklı') WHERE title<>'Existing fixture';");
            else
                await ExecutePack(db, "UPDATE speed_reading.exercises SET configuration_json='{}'::jsonb;");
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecutePack(db, sql));
            await db.Database.ExecuteSqlRawAsync("ROLLBACK;");
            Assert.Equal(textCount, await db.ReadingTexts.CountAsync());
            Assert.False(await db.ReadingTexts.AnyAsync(t => t.Tags == "guided-reading-v1"));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString).Options);

    private static async Task ExecutePack(OwnedSpeedReadingDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(JsonArray Settings, string Sql, Guid Student)> Seed(OwnedSpeedReadingDbContext db)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var path = Path.Combine(root!.FullName, "content-packs/guided-reading/v1");
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(path, "settings.json")))!.AsArray();
        var additions = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(path, "content.json")))!.AsArray();
        var sql = await File.ReadAllTextAsync(Path.Combine(path, "apply.sql"));
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "SpeedReading", "Hızlı Okuma", "word_highlight");
        db.ExerciseTypes.Add(type);
        foreach (var entry in settings)
        {
            var exerciseId = Guid.Parse(entry!["id"]!.GetValue<string>());
            var age = Guid.Parse(entry["ageGroupId"]!.GetValue<string>());
            var level = entry["level"]!.GetValue<int>();
            db.Exercises.Add(Exercise.Create(entry["title"]!.GetValue<string>(), "SpeedReading",
                entry["previousConfiguration"]!.ToJsonString(), level, student, type.Id, targetAgeGroupId: age, id: exerciseId));
            var minimum = entry["configuration"]!["engineConfig"]!["content"]!["minWordCount"]!.GetValue<int>();
            for (var index = 0; index < 2; index++)
                db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Existing fixture", string.Join(' ', Enumerable.Repeat("örnek", minimum + index)),
                    difficultyLevel: level, targetAgeGroupId: age));
        }
        // Only disposable fixture text hashes are substituted. The production SQL file is never modified.
        var seen = new HashSet<string>();
        foreach (var entry in additions)
        foreach (var source in entry!["sources"]?.AsArray() ?? [])
        {
            var id = source!["id"]!.GetValue<string>();
            if (!seen.Add(id)) continue;
            var setting = settings.Single(s => s!["id"]!.GetValue<string>() == entry["exerciseId"]!.GetValue<string>())!;
            var content = string.Join(' ', Enumerable.Repeat("kaynak", source["wordCount"]!.GetValue<int>()));
            var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
            sql = sql.Replace(source["contentHash"]!.GetValue<string>(), hash);
            db.ReadingTexts.Add(ReadingText.Create(Guid.Parse(id), source["title"]!.GetValue<string>(), content,
                difficultyLevel: entry["level"]!.GetValue<int>(), targetAgeGroupId: Guid.Parse(setting["ageGroupId"]!.GetValue<string>())));
        }
        await db.SaveChangesAsync();
        return (settings, sql, student);
    }
}
