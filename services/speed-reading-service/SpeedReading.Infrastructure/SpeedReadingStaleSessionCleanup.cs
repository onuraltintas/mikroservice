using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Configuration;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using ExerciseSessionStatus = SpeedReading.Domain.Sessions.ExerciseSessionStatus;

namespace SpeedReading.Infrastructure;

/// <summary>
/// Decision 12.11.2026. An exercise session that was left open (active or paused) and was not touched for
/// <see cref="SpeedReadingServiceOptions.StaleSessionHours"/> hours is closed as abandoned. Its end time is its last
/// activity plus the idle cap (a paused session ends at its pause), never the time of the clean-up, so the recorded
/// times stay sane. No result is written, so reports, daily progress, XP and assignments are not affected, and the
/// student can start the exercise again.
/// Assessment sessions are left alone: an assessment attempt allows one session per exercise
/// (ux_exercise_sessions_assessment_exercise), so an abandoned one could never be started again.
/// </summary>
public sealed class SpeedReadingStaleSessionProcessor(
    OwnedSpeedReadingDbContext db,
    TimeProvider clock,
    SpeedReadingServiceOptions options)
{
    public const int BatchSize = 100;

    /// <summary>One hourly run closes at most this many batches; a backlog is worked off over the next runs.</summary>
    public const int MaxBatchesPerRun = 20;

    /// <summary>A short rest between two batches, so a backlog does not load the database in one burst.</summary>
    public TimeSpan BatchDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddHours(-Math.Max(1, options.StaleSessionHours));
        var abandoned = 0;
        for (var round = 0; round < MaxBatchesPerRun && !cancellationToken.IsCancellationRequested; round++)
        {
            if (round > 0 && BatchDelay > TimeSpan.Zero)
                await Task.Delay(BatchDelay, cancellationToken);

            // updated_at is the row's concurrency token and changes with every save, so a row that was not
            // updated since the cut-off saw no activity since then. A closed session leaves this filter, so every
            // round reads the oldest sessions that are still open.
            var batch = await db.ExerciseSessions
                .Where(item => (item.Status == ExerciseSessionStatus.Active || item.Status == ExerciseSessionStatus.Paused)
                    && item.AssessmentAttemptId == null
                    && (item.UpdatedAt ?? item.StartTime) < cutoff)
                // ix_exercise_sessions_open covers this filter and order: (start_time, id) over open, non-assessment rows.
                .OrderBy(item => item.StartTime).ThenBy(item => item.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
                break;

            var closed = 0;
            foreach (var session in batch)
                session.Abandon(ResolveEndTime(session, now));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                closed = batch.Count;
            }
            catch (DbUpdateConcurrencyException)
            {
                // A student came back to one of these sessions while the batch was being saved.
                // Their change wins; the rest of the batch is saved one by one.
                db.ChangeTracker.Clear();
                closed = await AbandonOneByOneAsync(batch.Select(item => item.Id).ToList(), cutoff, now, cancellationToken);
            }
            db.ChangeTracker.Clear();
            abandoned += closed;
            // Stop on a short batch, and on a round that closed nothing (it cannot make progress).
            if (batch.Count < BatchSize || closed == 0)
                break;
        }

        return abandoned;
    }

    private async Task<int> AbandonOneByOneAsync(
        IReadOnlyList<Guid> ids,
        DateTime cutoff,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var abandoned = 0;
        foreach (var id in ids)
        {
            var session = await db.ExerciseSessions.SingleOrDefaultAsync(item => item.Id == id
                && (item.Status == ExerciseSessionStatus.Active || item.Status == ExerciseSessionStatus.Paused)
                && item.AssessmentAttemptId == null
                && (item.UpdatedAt ?? item.StartTime) < cutoff, cancellationToken);
            if (session is null)
                continue;

            session.Abandon(ResolveEndTime(session, now));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                abandoned++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Still in use; the next run looks at it again.
            }
            db.ChangeTracker.Clear();
        }

        return abandoned;
    }

    private static DateTime ResolveEndTime(ExerciseSession session, DateTime now)
    {
        var lastActivity = ReadLastActivity(session.SessionDataJson) ?? session.UpdatedAt ?? session.StartTime;
        if (lastActivity < session.StartTime)
            lastActivity = session.StartTime;
        return SpeedReadingActiveTimeRules.ResolveAbandonTime(
            lastActivity,
            session.Status == ExerciseSessionStatus.Paused ? session.PausedAt ?? lastActivity : null,
            now,
            SpeedReadingActiveTimeRules.MaxActiveGapSeconds);
    }

    /// <summary>The session state records the last exercise step, pause or resume the server saw.</summary>
    private static DateTime? ReadLastActivity(string stateJson)
    {
        try
        {
            using var state = JsonDocument.Parse(stateJson);
            return state.RootElement.ValueKind == JsonValueKind.Object
                && state.RootElement.TryGetProperty("lastActivityAt", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.TryGetDateTime(out var lastActivity)
                ? lastActivity.ToUniversalTime()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Runs the stale session clean-up once an hour.</summary>
public sealed class SpeedReadingStaleSessionWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<SpeedReadingStaleSessionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run after start-up settles, then hourly.
            await Task.Delay(TimeSpan.FromMinutes(10), clock, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var abandoned = await scope.ServiceProvider
                    .GetRequiredService<SpeedReadingStaleSessionProcessor>()
                    .RunAsync(stoppingToken);
                if (abandoned > 0)
                    logger.LogInformation("Closed {Count} stale speed-reading exercise sessions as abandoned.", abandoned);
                await Task.Delay(Interval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // The clean-up must never take down the API; the next cycle retries.
                logger.LogWarning(error, "Speed-reading stale session clean-up failed; the next cycle will retry.");
                try
                {
                    await Task.Delay(Interval, clock, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
