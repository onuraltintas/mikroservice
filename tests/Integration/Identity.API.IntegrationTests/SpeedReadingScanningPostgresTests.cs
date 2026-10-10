using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingScanningPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Graded_pack_is_atomic_idempotent_and_all_twenty_exercises_start(bool changedSource)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var pack = Path.Combine(directory!.FullName, "content-packs", "scanning", "v1");
        var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var sql = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Scanning", "Tarama", "scanning");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create(row.GetProperty("title").GetString()!, "Scanning",
                    changedSource ? "{}" : row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    student, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            await db.SaveChangesAsync();
            if (changedSource)
            {
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
                await db.Database.CloseConnectionAsync();
                Assert.Equal(0, await db.ReadingTexts.CountAsync());
                return;
            }
            await db.Database.ExecuteSqlRawAsync(sql);
            await db.Database.ExecuteSqlRawAsync(sql);
            db.ChangeTracker.Clear();
            Assert.Equal(60, await db.ReadingTexts.CountAsync());
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var started = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.Equal(row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("targetCount").GetInt32(), started.TotalSteps);
                var round = started.InitialData.GetProperty("scanningRounds")[0];
                Assert.NotEmpty(round.GetProperty("textContent").GetString()!);
                if (row.GetProperty("level").GetInt32() >= 3) Assert.NotEmpty(round.GetProperty("targetLabels").EnumerateObject());
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scanning_snapshot_and_results_survive_reload_without_client_scores(bool timeout)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the isolated disposable Testcontainers database is reset.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Scanning", "Tarama", "scanning");
            var exercise = Exercise.Create("Tarama", "reading", """{"engineConfig":{"targetCount":2,"timeLimit":10}}""", 3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Wrong", "başka seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Text", "ışık inci", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "scan_start" }));
            await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_start" });
            var click = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "scan_click", Index = 0, Number = 0 };
            await service.ValidateActionAsync(student, started.SessionId, click);
            db.ChangeTracker.Clear();
            await service.ValidateActionAsync(student, started.SessionId, click);
            var session = await db.ExerciseSessions.SingleAsync();
            Assert.Equal(1, session.CorrectCount);
            if (timeout)
            {
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-20);
                session.SetState(state.ToJsonString());
                await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_timeout" })).IsCompleted);
            }
            else await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 1, Number = 0 });
            db.ChangeTracker.Clear();
            var result = await service.CompleteAsync(student, started.SessionId, new() {
                CustomData = new Dictionary<string, JsonElement> { ["score"] = JsonSerializer.SerializeToElement(99999) }
            });
            Assert.Equal(timeout ? 50m : 100m, result.Accuracy);
            Assert.Null(result.RawWPM);
            Assert.Null(result.WordsRead);
            if (timeout) Assert.Equal(0, result.XpGained);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(result.Accuracy, repeated.Accuracy);
            Assert.Empty(await db.ReadingSessions.ToListAsync());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
