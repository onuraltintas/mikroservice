using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

/// <summary>
/// Review H4b (finding 22, decision 29.10.2026). Finished exercise sessions keep their raw working state (the whole
/// text, prepared items and the step replay cache) for <see cref="SessionStateDays"/> days; after that only the flags
/// that reports and learning paths still read are kept. Read notifications and campaign send logs are kept for
/// <see cref="MessageDays"/> days. Results, scores and answers live in their own tables and are never touched.
/// </summary>
public sealed class SpeedReadingDataRetentionProcessor(OwnedSpeedReadingDbContext db, TimeProvider clock)
{
    public const int SessionStateDays = 90;
    public const int MessageDays = 365;
    public const int BatchSize = 200;
    private const int CompactStateMaxLength = 200;
    private static readonly string[] KeptStateFlags = ["comprehensionMode", "readingIncomplete"];

    public async Task<SpeedReadingRetentionResult> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sessions = 0;
        int batch;
        do
        {
            batch = await CompactSessionsAsync(now.AddDays(-SessionStateDays), ct);
            sessions += batch;
        } while (batch == BatchSize && !ct.IsCancellationRequested);

        var messageCutoff = now.AddDays(-MessageDays);
        var notifications = await DeleteInBatchesAsync(() => ((ISpeedReadingDataContext)db).Notifications
            .Where(item => item.ReadAt != null && item.CreatedAt < messageCutoff), ct);
        var campaignLogs = await DeleteInBatchesAsync(() => ((ISpeedReadingDataContext)db).EmailCampaignLogs
            .Where(item => (item.SentAt ?? item.CreatedAt) < messageCutoff), ct);
        return new SpeedReadingRetentionResult(sessions, notifications, campaignLogs);
    }

    private async Task<int> CompactSessionsAsync(DateTime cutoff, CancellationToken ct)
    {
        var due = await db.ExerciseSessions
            .Where(item => (item.Status == ExerciseSessionStatus.Completed
                    || item.Status == ExerciseSessionStatus.Timeout
                    || item.Status == ExerciseSessionStatus.Abandoned)
                && item.EndTime != null && item.EndTime < cutoff
                && (item.SessionDataJson.Length > CompactStateMaxLength || item.ProcessedActionsJson != "{}"))
            .OrderBy(item => item.EndTime).ThenBy(item => item.Id)
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var session in due)
        {
            session.SetState(CompactState(session.SessionDataJson), session.CustomDataJson);
            session.SetProcessedActions("{}");
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return due.Count;
    }

    internal static string CompactState(string stateJson)
    {
        var compact = new JsonObject();
        try
        {
            if (JsonNode.Parse(stateJson) is JsonObject state)
            {
                foreach (var flag in KeptStateFlags)
                    if (state[flag] is { } value)
                        compact[flag] = value.DeepClone();
            }
        }
        catch (JsonException)
        {
            // Historical non-JSON state carries none of the kept flags.
        }
        return compact.ToJsonString();
    }

    private async Task<int> DeleteInBatchesAsync<T>(Func<IQueryable<T>> query, CancellationToken ct) where T : class
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            var rows = await query().Take(BatchSize).ToListAsync(ct);
            if (rows.Count == 0) break;
            db.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }
}

public sealed record SpeedReadingRetentionResult(int CompactedSessions, int DeletedNotifications, int DeletedCampaignLogs);

public sealed class SpeedReadingDataRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<SpeedReadingDataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run after start-up settles, then daily.
        await Task.Delay(TimeSpan.FromMinutes(15), clock, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<SpeedReadingDataRetentionProcessor>().RunAsync(stoppingToken);
                if (result.CompactedSessions + result.DeletedNotifications + result.DeletedCampaignLogs > 0)
                    logger.LogInformation(
                        "Speed Reading retention: compacted {Sessions} sessions, deleted {Notifications} notifications and {Logs} campaign logs",
                        result.CompactedSessions, result.DeletedNotifications, result.DeletedCampaignLogs);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Speed Reading retention failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromDays(1), clock, stoppingToken);
        }
    }
}
