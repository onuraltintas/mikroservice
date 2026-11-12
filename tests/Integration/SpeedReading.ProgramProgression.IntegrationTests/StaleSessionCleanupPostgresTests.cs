using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Configuration;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

/// <summary>
/// Decision 12.11.2026: exercise sessions left open for a day are closed as abandoned. This runs the clean-up
/// against PostgreSQL: the query, the batches, and a student who returns while a batch is being saved.
/// </summary>
public sealed class StaleSessionCleanupPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private OwnedSpeedReadingDbContext Open(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString, b => b.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"));
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new OwnedSpeedReadingDbContext(builder.Options);
    }

    [Fact]
    public async Task Stale_open_sessions_are_abandoned_at_their_last_activity_plus_the_cap()
    {
        var exerciseId = await Reset();
        var start = Now.AddDays(-5);
        var active = Session(exerciseId, ExerciseSessionStatus.Active, start, lastActivity: start.AddMinutes(3));
        var legacy = Session(exerciseId, ExerciseSessionStatus.Active, start, lastActivity: null, updatedAt: start.AddMinutes(8));
        var paused = Session(exerciseId, ExerciseSessionStatus.Paused, start, lastActivity: start.AddMinutes(6), pausedAt: start.AddMinutes(6));
        var recent = Session(exerciseId, ExerciseSessionStatus.Active, Now.AddHours(-23), lastActivity: Now.AddHours(-23));
        var completed = Session(exerciseId, ExerciseSessionStatus.Completed, start, lastActivity: start, endTime: start.AddMinutes(2));
        await using (var seed = Open())
        {
            seed.ExerciseSessions.AddRange(active, legacy, paused, recent, completed);
            for (var index = 0; index < SpeedReadingStaleSessionProcessor.BatchSize + 5; index++)
                seed.ExerciseSessions.Add(Session(exerciseId, ExerciseSessionStatus.Active, start, lastActivity: start.AddSeconds(index)));
            await seed.SaveChangesAsync();
        }

        await using (var db = Open())
            Assert.Equal(SpeedReadingStaleSessionProcessor.BatchSize + 8, await Processor(db).RunAsync(CancellationToken.None));

        await using (var check = Open())
        {
            var rows = await check.ExerciseSessions.AsNoTracking().ToDictionaryAsync(item => item.Id);
            Assert.Equal(ExerciseSessionStatus.Abandoned, rows[active.Id].Status);
            Assert.Equal(start.AddMinutes(8), rows[active.Id].EndTime);
            Assert.Equal(start.AddMinutes(13), rows[legacy.Id].EndTime);
            Assert.Equal(ExerciseSessionStatus.Abandoned, rows[paused.Id].Status);
            Assert.Equal(start.AddMinutes(6), rows[paused.Id].EndTime);
            Assert.Equal(0, rows[paused.Id].TotalPausedSeconds);
            Assert.Equal(ExerciseSessionStatus.Active, rows[recent.Id].Status);
            Assert.Equal(ExerciseSessionStatus.Completed, rows[completed.Id].Status);
            Assert.Equal(start.AddMinutes(2), rows[completed.Id].EndTime);
            Assert.Equal(1, rows.Values.Count(item => item.Status == ExerciseSessionStatus.Active));
        }

        await using (var again = Open())
            Assert.Equal(0, await Processor(again).RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_student_who_returns_while_the_batch_is_saved_keeps_the_session()
    {
        var exerciseId = await Reset();
        var start = Now.AddDays(-2);
        var returning = Session(exerciseId, ExerciseSessionStatus.Active, start, lastActivity: start.AddMinutes(1));
        var others = Enumerable.Range(0, 3)
            .Select(index => Session(exerciseId, ExerciseSessionStatus.Active, start, lastActivity: start.AddMinutes(2 + index))).ToList();
        await using (var seed = Open())
        {
            seed.ExerciseSessions.Add(returning);
            seed.ExerciseSessions.AddRange(others);
            await seed.SaveChangesAsync();
        }

        var studentReturns = new BeforeFirstSave(async () =>
        {
            await using var student = Open();
            var session = await student.ExerciseSessions.SingleAsync(item => item.Id == returning.Id);
            session.SetCurrentStep(2);
            await student.SaveChangesAsync();
        });
        await using (var db = Open(studentReturns))
            Assert.Equal(3, await Processor(db).RunAsync(CancellationToken.None));

        await using var check = Open();
        var rows = await check.ExerciseSessions.AsNoTracking().ToDictionaryAsync(item => item.Id);
        Assert.Equal(ExerciseSessionStatus.Active, rows[returning.Id].Status);
        Assert.Equal(2, rows[returning.Id].CurrentStep);
        Assert.Null(rows[returning.Id].EndTime);
        Assert.All(others, item => Assert.Equal(ExerciseSessionStatus.Abandoned, rows[item.Id].Status));
    }

    private async Task<Guid> Reset()
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var type = ExerciseType.Create(Guid.NewGuid(), "VisualExpansion", "Görsel Genişleme", "visual_expansion");
        var exercise = Exercise.Create("Görsel Genişleme", "VisualExpansion", "{\"engineType\":\"visual_expansion\"}", 1, Guid.NewGuid(), type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise.Id;
    }

    private static SpeedReadingStaleSessionProcessor Processor(OwnedSpeedReadingDbContext db) =>
        new(db, new FixedClock(Now), new SpeedReadingServiceOptions());

    private static ExerciseSession Session(
        Guid exerciseId,
        ExerciseSessionStatus status,
        DateTime start,
        DateTime? lastActivity,
        DateTime? pausedAt = null,
        DateTime? endTime = null,
        DateTime? updatedAt = null)
    {
        var state = lastActivity.HasValue
            ? JsonSerializer.Serialize(new { engineType = "visual_expansion", lastActivityAt = lastActivity.Value })
            : "{\"engineType\":\"visual_expansion\"}";
        return ExerciseSession.Import(Guid.NewGuid(), Guid.NewGuid(), exerciseId, null, null, status, start, endTime,
            0, pausedAt, null, 1, 5, 1, 0, state, null, "{}", start, null, updatedAt ?? lastActivity, null);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class BeforeFirstSave(Func<Task> action) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await action();
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
