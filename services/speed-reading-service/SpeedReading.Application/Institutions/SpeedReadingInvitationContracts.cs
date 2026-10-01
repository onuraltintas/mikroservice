using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.Institutions;

public enum SpeedReadingInvitationCreateResult
{
    Created,
    AlreadyPending,
    InvalidScope,
    MembershipRequired,
    ProductRoleRequired
}

public enum SpeedReadingInvitationAcceptResult
{
    Accepted,
    AlreadyAccepted,
    TeacherAssignmentConflict,
    NotFound,
    WrongEmail,
    Expired,
    NotPending,
    ProductRoleRequired,
    MembershipRequired,
    InvalidInvitation
}

public enum SpeedReadingInvitationCancelResult
{
    Cancelled,
    NotFound,
    NotPending
}

public sealed record SpeedReadingPendingInvitation(
    Guid InvitationId,
    string Email,
    string Role,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public sealed record SpeedReadingInvitationRecord(
    Guid InvitationId,
    string Email,
    SpeedReadingInstitutionMemberRole Role,
    Guid? InstitutionId,
    Guid? TeacherUserId,
    Guid InvitedByUserId,
    SpeedReadingInvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? AcceptedByUserId,
    DateTime? AcceptedAt);

public sealed record SpeedReadingInvitationCreateResponse(
    SpeedReadingInvitationCreateResult Result,
    SpeedReadingInvitationRecord? Invitation);

public interface ISpeedReadingInvitations
{
    Task<SpeedReadingInvitationCreateResponse> CreateAsync(
        string email,
        SpeedReadingInstitutionMemberRole role,
        Guid? institutionId,
        Guid? teacherUserId,
        Guid invitedByUserId,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingInvitationAcceptResult> AcceptAsync(
        Guid invitationId,
        Guid userId,
        string email,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpeedReadingPendingInvitation>> GetPendingByInviterAsync(
        Guid inviterUserId,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingInvitationCancelResult> CancelAsync(
        Guid invitationId,
        Guid inviterUserId,
        DateTime at,
        CancellationToken cancellationToken = default);
}
