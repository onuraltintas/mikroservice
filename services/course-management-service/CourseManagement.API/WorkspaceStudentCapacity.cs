using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

internal static class WorkspaceStudentCapacity
{
    public static async Task LockWorkspaceAsync(CourseManagementDbContext db, Guid workspaceId, CancellationToken ct)
    {
        await db.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"Workspaces\" WHERE \"Id\" = {workspaceId} FOR UPDATE")
            .SingleAsync(ct);
    }

    public static async Task<bool> CanAddAsync(CourseManagementDbContext db, Guid workspaceId, int additionalStudents,
        CancellationToken ct)
    {
        if (additionalStudents < 1) return true;

        // Expiry and access-state write gating remains a product decision; this enforces the configured count only.
        var limit = await db.WorkspaceSubscriptions.AsNoTracking()
            .Where(subscription => subscription.WorkspaceId == workspaceId)
            .Select(subscription => (int?)subscription.ActiveStudentLimit)
            .SingleOrDefaultAsync(ct);
        if (limit is null) return true;

        var activeCount = await db.Students.AsNoTracking()
            .CountAsync(student => student.WorkspaceId == workspaceId && student.IsActive, ct);
        return activeCount <= limit.Value - additionalStudents;
    }
}
