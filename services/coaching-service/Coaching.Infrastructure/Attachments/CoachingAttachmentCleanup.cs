using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Attachments;

/// <summary>
/// Removes student file records that never completed: metadata created but no upload within its window,
/// and legacy rows left "in scan" by older versions when the scanner failed. Their bytes (if any) are deleted.
/// </summary>
public sealed class CoachingAttachmentCleanupProcessor(CoachingDbContext db, IAssignmentAttachmentStorage storage,
    TimeProvider clock, ILogger<CoachingAttachmentCleanupProcessor> logger)
{
    public const int BatchSize = 200;

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiredWindow = now.AddHours(-1);
        var neverTicketed = now.AddDays(-1);
        var stale = await db.Set<AssignmentSubmissionAttachment>()
            .Where(x => x.Status == AttachmentScanStatus.PendingUpload
                    && (x.UploadExpiresAt == null ? x.CreatedAt < neverTicketed : x.UploadExpiresAt < expiredWindow)
                || x.Status == AttachmentScanStatus.PendingScan && x.UploadedAt < expiredWindow)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(BatchSize).ToListAsync(ct);
        if (stale.Count == 0) return 0;

        db.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        foreach (var attachment in stale)
        {
            try { await storage.DeleteAsync(attachment.StorageKey, ct); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogWarning(error, "Stale attachment {AttachmentId} file could not be deleted", attachment.Id); }
        }
        logger.LogInformation("Removed {Count} incomplete assignment attachment uploads", stale.Count);
        return stale.Count;
    }
}

public sealed class CoachingAttachmentCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<CoachingAttachmentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int removed;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    removed = await scope.ServiceProvider.GetRequiredService<CoachingAttachmentCleanupProcessor>().CleanupAsync(stoppingToken);
                } while (removed == CoachingAttachmentCleanupProcessor.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Attachment cleanup failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}
