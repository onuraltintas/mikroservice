using System.Net.Mail;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Profiles;

public enum CoachingInvitationStatus { Pending, Accepted, Rejected, Cancelled, Expired }

public sealed class CoachingInvitation : AggregateRoot
{
    private CoachingInvitation() { }
    public Guid InviterUserId { get; private set; }
    public string RecipientEmail { get; private set; } = "";
    public string Role { get; private set; } = "";
    public Guid? InstitutionId { get; private set; }
    public Guid? TeacherUserId { get; private set; }
    public bool IsInstitutionInvitation { get; private set; }
    public string? Message { get; private set; }
    public CoachingInvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public string Type => IsInstitutionInvitation ? Role == "Teacher" ? "TeacherToInstitution" : "StudentToInstitution" : "StudentToTeacher";

    public static CoachingInvitation Create(Guid inviterUserId, string email, string role, Guid? institutionId,
        Guid? teacherUserId, bool institutionInvitation, string? message, DateTime now)
    {
        var normalized = NormalizeEmail(email);
        if (inviterUserId == Guid.Empty || role is not ("Student" or "Teacher") || institutionId == Guid.Empty
            || teacherUserId == Guid.Empty || institutionInvitation && !institutionId.HasValue
            || !institutionInvitation && (role != "Student" || !teacherUserId.HasValue)
            || role == "Teacher" && teacherUserId.HasValue || message?.Length > 2000)
            throw new ArgumentException("Geçersiz Koçluk daveti.");
        return new() { InviterUserId = inviterUserId, RecipientEmail = normalized, Role = role, InstitutionId = institutionId,
            TeacherUserId = teacherUserId, IsInstitutionInvitation = institutionInvitation,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(), CreatedAt = now, ExpiresAt = now.AddDays(7), CreatedBy = inviterUserId.ToString() };
    }

    public static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new ArgumentException("Geçerli e-posta adresi girin.");
        return normalized;
    }

    public void Finish(CoachingInvitationStatus status, Guid actorUserId, DateTime now)
    {
        if (Status != CoachingInvitationStatus.Pending || status == CoachingInvitationStatus.Pending || !Enum.IsDefined(status))
            throw new InvalidOperationException("Davet artık beklemiyor.");
        Status = status; UpdatedAt = now; UpdatedBy = actorUserId.ToString();
        if (status == CoachingInvitationStatus.Accepted) AcceptedByUserId = actorUserId;
    }
}
