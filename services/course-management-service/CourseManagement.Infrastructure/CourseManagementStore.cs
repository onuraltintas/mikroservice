using CourseManagement.Application;
using CourseManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure;

public sealed class CourseManagementStore(CourseManagementDbContext db) : ICourseManagementStore
{
    public Task<CourseUserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserProfiles.SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task SaveProfileAsync(CourseUserProfile profile, CancellationToken cancellationToken)
    {
        if (db.Entry(profile).State == EntityState.Detached) db.UserProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> GetOwnedWorkspacesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Workspaces.AsNoTracking().Where(workspace => workspace.OwnerUserId == userId)
            .OrderBy(workspace => workspace.Name).ToListAsync(cancellationToken);
}
