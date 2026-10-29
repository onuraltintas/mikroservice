using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingTrackingPackPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("none")]
    [InlineData("catalog")]
    [InlineData("duplicate")]
    [InlineData("update")]
    [InlineData("rollback")]
    public async Task Pack_is_atomic_idempotent_runs_twenty_cells_and_preserves_history(string drift)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        var pack = Path.Combine(root!.FullName, "content-packs", "eye-tracking", "v1");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        var apply = await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql"));
        var rollback = await File.ReadAllTextAsync(Path.Combine(pack, "rollback.sql"));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers database, never an application/live connection.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "EyeTracking", "Göz Takibi", "motion_path");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create("Göz Takibi", "EyeTracking", drift == "catalog" ? "{}"
                    : row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(),
                    creator, type.Id, row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            if (drift == "duplicate")
                db.Exercises.Add(Exercise.Create("Mükerrer", "EyeTracking", "{}", 1, creator, type.Id,
                    targetAgeGroupId: settings.RootElement[0].GetProperty("ageGroupId").GetGuid()));
            var oldSession = ExerciseSession.Start(creator, settings.RootElement[0].GetProperty("id").GetGuid(), null, 1, DateTime.UtcNow, null);
            oldSession.SetState("{\"oldSnapshot\":true}"); db.ExerciseSessions.Add(oldSession);
            await db.SaveChangesAsync();
            var before = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.ConfigurationJson);
            if (drift == "update")
                await Execute("""
                    CREATE FUNCTION speed_reading.reject_tracking_update() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN RAISE EXCEPTION 'EyeTracking injected update failure'; END $$;
                    CREATE TRIGGER reject_tracking_update BEFORE UPDATE ON speed_reading.exercises
                    FOR EACH ROW EXECUTE FUNCTION speed_reading.reject_tracking_update();
                    """);
            if (drift is "catalog" or "duplicate" or "update")
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(apply));
                Assert.Contains("EyeTracking", error.MessageText);
                foreach (var item in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(before[item.Id], item.ConfigurationJson);
                return;
            }
            await Execute(apply); db.ChangeTracker.Clear();
            var versions = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => (item.Version, item.UpdatedAt));
            await Execute(apply); db.ChangeTracker.Clear();
            foreach (var item in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(versions[item.Id], (item.Version, item.UpdatedAt));
            if (drift == "rollback")
            {
                var edited = await db.Exercises.FirstAsync();
                edited.Update(edited.Title, edited.Description, edited.TypeCode, "{}", edited.DifficultyLevel,
                    edited.ExerciseTypeId, edited.TargetAgeGroupId, creator, DateTime.UtcNow);
                await db.SaveChangesAsync();
                var after = await db.Exercises.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.ConfigurationJson);
                await Assert.ThrowsAsync<PostgresException>(() => Execute(rollback));
                foreach (var item in await db.Exercises.AsNoTracking().ToListAsync()) Assert.Equal(after[item.Id], item.ConfigurationJson);
                return;
            }
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid();
                var start = await service.StartAsync(user, new() { ExerciseId = row.GetProperty("id").GetGuid() });
                Assert.Equal("tracking", start.InitialData.GetProperty("textStreamMode").GetString());
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("configuration").GetRawText()), JsonNode.Parse(start.Configuration.GetRawText())));
                await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(user, start.SessionId, new()));
                await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), start.SessionId, new()));
                Assert.True((await service.ValidateActionAsync(user, start.SessionId, new() { Action = "tracking_start" })).IsValid);
                var session = await db.ExerciseSessions.SingleAsync(item => item.Id == start.SessionId);
                var seconds = row.GetProperty("configuration").GetProperty("engineConfig").GetProperty("timing").GetProperty("durationSeconds").GetInt32();
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-seconds - 1);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                session.Pause(DateTime.UtcNow.AddSeconds(-seconds)); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(user, start.SessionId, new()));
                session.Resume(DateTime.UtcNow);
                state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-2 * seconds - 2);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
                var result = await service.CompleteAsync(user, start.SessionId, new());
                Assert.Equal("NotMeasured", result.MeasurementStatus);
                Assert.Null(result.Score); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Null(result.WeightedKDP);
                await service.CompleteAsync(user, start.SessionId, new());
                Assert.Equal(1, await db.ExerciseSessionResults.CountAsync(item => item.SessionId == start.SessionId));
            }
            await Execute(rollback); db.ChangeTracker.Clear();
            await Execute(rollback); db.ChangeTracker.Clear();
            Assert.Equal(20, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(0, await db.ReadingSessions.CountAsync());
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"oldSnapshot\":true}"),
                JsonNode.Parse((await db.ExerciseSessions.SingleAsync(item => item.Id == oldSession.Id)).SessionDataJson)));
            foreach (var item in await db.Exercises.AsNoTracking().ToListAsync())
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before[item.Id]), JsonNode.Parse(item.ConfigurationJson)));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
