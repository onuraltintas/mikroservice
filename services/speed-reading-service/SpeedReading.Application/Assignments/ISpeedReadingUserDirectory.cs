using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.Application.Assignments;

public interface ISpeedReadingUserDirectory
{
    Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
