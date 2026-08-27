using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.Application.Assignments;

public interface ISpeedReadingUserDirectory
{
    Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(
        string? role,
        CancellationToken cancellationToken = default);
}
