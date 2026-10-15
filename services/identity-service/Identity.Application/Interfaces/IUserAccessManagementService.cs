using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;

namespace Identity.Application.Interfaces;

public sealed record UserSessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? CreatedByIp,
    bool IsPersistent,
    DateTimeOffset? MfaVerifiedAt);

public interface IUserAccessManagementService
{
    Task<Result> SetProductAccessAsync(
        Guid userId,
        PlatformProduct product,
        bool isActive,
        Guid grantedByUserId,
        string? revokedByIp,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<UserSessionDto>>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? revokedByIp,
        CancellationToken cancellationToken);

    Task<Result> RevokeAllSessionsAsync(
        Guid userId,
        string? revokedByIp,
        string reason,
        CancellationToken cancellationToken);

    Task<Result> ResetMfaAsync(
        Guid userId,
        string? revokedByIp,
        CancellationToken cancellationToken);
}
