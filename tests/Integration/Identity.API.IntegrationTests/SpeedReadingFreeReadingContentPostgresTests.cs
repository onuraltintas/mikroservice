using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingFreeReadingContentPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Approved_pack_runs_all_twenty_question_free_cells_in_owned_schema()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "free-reading", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Exclusively the disposable Testcontainers database, never production.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "FreeReading", "Serbest Okuma", "free_reading"); db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray().Where(row => row.GetProperty("previousConfiguration").ValueKind != JsonValueKind.Null))
                db.Exercises.Add(Exercise.Create(row.GetProperty("title").GetString()!, "FreeReading",
                    row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(), creator, type.Id,
                    row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            await db.SaveChangesAsync();
            await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql")), connection);
                await command.ExecuteNonQueryAsync(); await command.ExecuteNonQueryAsync();
            }
            db.ChangeTracker.Clear(); Assert.Equal(20, await db.Exercises.CountAsync()); Assert.Equal(100, await db.ReadingTexts.CountAsync());
            Assert.Empty(await db.ReadingQuestions.ToListAsync());
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid(); var exerciseId = row.GetProperty("id").GetGuid();
                var started = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                Assert.Empty(started.InitialData.GetProperty("questions").EnumerateArray());
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(row.GetProperty("ageGroupId").GetGuid(), text.TargetAgeGroupId);
                Assert.Equal(row.GetProperty("level").GetInt32(), text.DifficultyLevel);
                var band = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("content");
                Assert.InRange(text.WordCount, band.GetProperty("minWordCount").GetInt32(), band.GetProperty("maxWordCount").GetInt32());
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddMinutes(-5); session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                var result = await service.CompleteAsync(user, session.Id, new());
                Assert.NotNull(result.RawWPM); Assert.Null(result.Score); Assert.Null(result.ComprehensionScore); Assert.Equal(0, result.XpGained);
            }
            Assert.Equal(20, await db.ExerciseSessionResults.CountAsync()); Assert.Empty(await db.UserGamifications.ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
