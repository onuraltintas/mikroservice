namespace EduPlatform.Shared.Contracts.Reporting;

public sealed record SpeedReadingUserDirectoryRequest(
    IReadOnlyCollection<Guid> UserIds);

public sealed record SpeedReadingUserDirectoryItem(
    Guid UserId,
    string FirstName,
    string LastName,
    bool IsActive);

public sealed record SpeedReadingUserDirectoryResponse(
    IReadOnlyList<SpeedReadingUserDirectoryItem> Users);
