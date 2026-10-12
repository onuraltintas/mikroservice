using CourseManagement.Domain;

namespace CourseManagement.Application;

public interface ICourseManagementStore
{
    Task<CourseUserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveProfileAsync(CourseUserProfile profile, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetOwnedWorkspacesAsync(Guid userId, CancellationToken cancellationToken);
}
