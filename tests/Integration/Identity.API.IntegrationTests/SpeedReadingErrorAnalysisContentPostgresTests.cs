using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Content;
using SpeedReading.Application.StudentReading;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingErrorAnalysisContentPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Prepared_pack_runs_all_eight_cells_and_preserves_session_snapshots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "error-analysis", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture is an isolated Testcontainers database, never a configured application database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray().DistinctBy(row => row.GetProperty("ageGroupId").GetGuid()))
            {
                var age = row.GetProperty("age").GetString()!;
                var minimum = age switch { "child" => 9, "teen" => 13, "young-adult" => 17, _ => 22 };
                db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(row.GetProperty("ageGroupId").GetGuid(), age, age,
                    minimum, age == "adult" ? null : minimum + (age == "child" || age == "teen" ? 3 : 4),
                    50, 150, 1000, 70, 15, 1, minimum, true, null, creator, DateTime.UtcNow));
            }
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create(row.GetProperty("title").GetString()!, "ErrorAnalysis",
                    row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(), creator, type.Id,
                    row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            // An already-started static session must survive publishing the new content package.
            var legacy = Exercise.Create("Legacy fixture", "ErrorAnalysis", """
                {"engineType":"error_analysis","engineConfig":{"words":[{"index":0,"text":"yanlız"}],
                "errors":[{"wordIndex":0,"originalWord":"yalnız","errorWord":"yanlız"}]}}
                """, 4, creator, type.Id);
            db.Exercises.Add(legacy); await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var old = await service.StartAsync(creator, new() { ExerciseId = legacy.Id });
            var snapshot = (await db.ExerciseSessions.SingleAsync(s => s.Id == old.SessionId)).SessionDataJson;
            await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql")), connection);
                await command.ExecuteNonQueryAsync(); await command.ExecuteNonQueryAsync();
            }
            db.ChangeTracker.Clear(); Assert.Equal(9, await db.Exercises.CountAsync()); Assert.Equal(40, await db.ReadingTexts.CountAsync());
            Assert.Empty(await db.ReadingQuestions.ToListAsync());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(snapshot),
                JsonSerializer.Deserialize<JsonElement>((await db.ExerciseSessions.SingleAsync(s => s.Id == old.SessionId)).SessionDataJson)));
            var catalog = (ILegacySpeedReadingCatalog)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalog")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var reading = (ISpeedReadingStudentReading)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentReading")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid(); var exerciseId = row.GetProperty("id").GetGuid();
                db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, row.GetProperty("level").GetInt32(),
                    200, 70, 15, row.GetProperty("ageGroupId").GetGuid(), null, true, DateTime.UtcNow, null, null, null));
                await db.SaveChangesAsync();
                var started = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                Assert.DoesNotContain("originalWord", started.InitialData.GetRawText());
                Assert.DoesNotContain("errorsByTextId", started.Configuration.GetRawText());
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(row.GetProperty("ageGroupId").GetGuid(), text.TargetAgeGroupId);
                Assert.Equal(row.GetProperty("level").GetInt32(), text.DifficultyLevel);
                Assert.Null(await catalog.GetReadingTextAsync(text.Id, true, false, false, user));
                Assert.Empty(await catalog.GetReadingTextsAsync(exerciseId, null, null, null, false, null, true, user));
                Assert.DoesNotContain(await catalog.GetShortReadingTextsAsync(50, user), item => item.Id == text.Id);
                Assert.NotNull(await catalog.GetReadingTextAsync(text.Id, false, false, false, null));
                Assert.Null(await reading.StartAsync(user, text.Id, CancellationToken.None));
                Assert.Empty(await reading.GetAvailableTextsAsync(user, null, null, null, null, CancellationToken.None));
                var config = row.GetProperty("configuration").GetProperty("engineConfig");
                var band = config.GetProperty("content");
                Assert.InRange(text.WordCount, band.GetProperty("minWordCount").GetInt32(), band.GetProperty("maxWordCount").GetInt32());
                Assert.Equal(config.GetProperty("errorCount").GetInt32(), started.TotalSteps);
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "error_analysis_start" })).IsValid);
                foreach (var target in config.GetProperty("errorsByTextId").GetProperty(text.Id.ToString()).EnumerateArray())
                    Assert.True((await service.ValidateActionAsync(user, session.Id,
                        new() { Action = "error_analysis_select", Index = target.GetProperty("wordIndex").GetInt32() })).IsValid);
                var result = await service.CompleteAsync(user, session.Id, new());
                var replay = await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(100m, result.Score); Assert.Equal(100m, result.Accuracy);
                Assert.Null(result.RawWPM); Assert.Null(result.ComprehensionScore); Assert.Equal(result.Score, replay.Score);
            }
            Assert.Equal(8, await db.ExerciseSessionResults.CountAsync());
            Assert.True((await service.ValidateActionAsync(creator, old.SessionId, new() { Action = "error_analysis_start" })).IsValid);
            Assert.True((await service.ValidateActionAsync(creator, old.SessionId, new() { Action = "error_analysis_select", Index = 0 })).IsCompleted);
            Assert.Equal(100m, (await service.CompleteAsync(creator, old.SessionId, new())).Score);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
