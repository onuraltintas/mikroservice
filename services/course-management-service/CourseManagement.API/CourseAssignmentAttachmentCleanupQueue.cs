using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;

namespace CourseManagement.API;

public sealed class CourseAssignmentAttachmentCleanupQueue(IServiceScopeFactory scopeFactory,
    ILogger<CourseAssignmentAttachmentCleanupQueue> logger)
{
    public async Task DeleteOrQueueAsync(ICourseAssignmentAttachmentStorage storage, string storageKey)
    {
        try
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception deletionException)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
                db.AttachmentCleanupRequests.Add(CourseAssignmentAttachmentCleanupRequest.Create(storageKey,
                    DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(CancellationToken.None);
                logger.LogError(deletionException,
                    "Could not remove course assignment attachment; a retry was queued.");
            }
            catch (Exception queueException)
            {
                CourseManagementOperationMetrics.RecordAttachmentCleanupQueueFailure();
                logger.LogCritical(queueException,
                    "Could not queue course assignment attachment cleanup after file deletion failed.");
                throw;
            }
        }
    }
}
