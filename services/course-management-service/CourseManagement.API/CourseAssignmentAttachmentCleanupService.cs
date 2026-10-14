using CourseManagement.Application.CourseAssignments;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed class CourseAssignmentAttachmentCleanupService(IServiceScopeFactory scopeFactory,
    ILogger<CourseAssignmentAttachmentCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCleanupOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Course assignment attachment cleanup failed."); }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    internal async Task RunCleanupOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<ICourseAssignmentAttachmentStorage>();
        var expired = await db.AssignmentAttachments.Where(item => item.ExpiresAt <= DateTimeOffset.UtcNow)
            .OrderBy(item => item.ExpiresAt).Take(100).ToListAsync(cancellationToken);

        foreach (var attachment in expired)
        {
            try
            {
                await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
                db.AssignmentAttachments.Remove(attachment);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                db.Entry(attachment).State = EntityState.Unchanged;
                logger.LogError(exception, "Could not delete expired course attachment {AttachmentId}.", attachment.Id);
            }
        }

        var pending = await db.AttachmentCleanupRequests.OrderBy(item => item.QueuedAt).Take(100)
            .ToListAsync(cancellationToken);
        foreach (var request in pending)
        {
            try
            {
                await storage.DeleteAsync(request.StorageKey, cancellationToken);
                db.AttachmentCleanupRequests.Remove(request);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                db.Entry(request).State = EntityState.Unchanged;
                logger.LogError(exception, "Could not retry course attachment cleanup request {CleanupRequestId}.",
                    request.Id);
            }
        }
    }
}
