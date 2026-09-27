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
}
