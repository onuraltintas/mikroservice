using CourseManagement.Domain;

namespace CourseManagement.Infrastructure.Privacy;

public interface ICourseManagementLegalHoldStatusProvider
{
    Task<CourseManagementLegalHoldStatus> GetStatusAsync(
        Guid requestId,
        Guid subjectUserId,
        CancellationToken cancellationToken);
}

public sealed class UnverifiedCourseManagementLegalHoldStatusProvider : ICourseManagementLegalHoldStatusProvider
{
    public Task<CourseManagementLegalHoldStatus> GetStatusAsync(
        Guid requestId,
        Guid subjectUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CourseManagementLegalHoldStatus.Unknown);
    }
}
