using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Institutions;

public enum SpeedReadingInvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Expired = 3
}

/// <summary>
/// A product-owned invitation. The optional institution distinguishes a
/// school relationship from a standalone teacher's direct student link.
/// </summary>
public sealed class SpeedReadingInvitation : AggregateRoot
{
    private SpeedReadingInvitation()
    {
    }

    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DeduplicationKey { get; private set; } = string.Empty;
    public SpeedReadingInstitutionMemberRole Role { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public Guid? TeacherUserId { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public SpeedReadingInvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    public static SpeedReadingInvitation Create(
        string email,
        SpeedReadingInstitutionMemberRole role,
        Guid? institutionId,
        Guid? teacherUserId,
        Guid invitedByUserId,
        DateTime at)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length is 0 or > 320 || !normalizedEmail.Contains('@'))
            throw new ArgumentException("A valid email address is required.", nameof(email));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (institutionId == Guid.Empty || teacherUserId == Guid.Empty)
            throw new ArgumentException("Institution and teacher identifiers cannot be empty.");
        if (invitedByUserId == Guid.Empty)
            throw new ArgumentException("Invitation actor is required.", nameof(invitedByUserId));
        if (role == SpeedReadingInstitutionMemberRole.Teacher
            && (!institutionId.HasValue || teacherUserId.HasValue))
        {
            throw new ArgumentException("Teacher invitations must be institution-scoped and cannot target another teacher.");
        }
        if (role == SpeedReadingInstitutionMemberRole.Student
            && (!institutionId.HasValue && teacherUserId != invitedByUserId))
        {
            throw new ArgumentException("Standalone student invitations must be sent by the assigned teacher.");
        }

        at = EnsureUtc(at);
        return new SpeedReadingInvitation
        {
            NormalizedEmail = normalizedEmail,
            DeduplicationKey = CreateDeduplicationKey(normalizedEmail, role, institutionId, teacherUserId),
            Role = role,
            InstitutionId = institutionId,
            TeacherUserId = teacherUserId,
            InvitedByUserId = invitedByUserId,
            Status = SpeedReadingInvitationStatus.Pending,
            CreatedAt = at,
            CreatedBy = invitedByUserId.ToString(),
            ExpiresAt = at.AddDays(7)
        };
    }

    public static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToUpperInvariant();

    public bool IsForEmail(string email) =>
        NormalizedEmail == NormalizeEmail(email);

    public void MarkAccepted(Guid userId, DateTime at)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Accepting user is required.", nameof(userId));
        if (Status != SpeedReadingInvitationStatus.Pending)
            throw new InvalidOperationException("Only a pending invitation can be accepted.");

        Status = SpeedReadingInvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = EnsureUtc(at);
        UpdatedAt = AcceptedAt;
        UpdatedBy = userId.ToString();
    }

    public void MarkExpired(DateTime at)
    {
        if (Status != SpeedReadingInvitationStatus.Pending)
            return;

        Status = SpeedReadingInvitationStatus.Expired;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = "system";
    }

    private static string CreateDeduplicationKey(
        string normalizedEmail,
        SpeedReadingInstitutionMemberRole role,
        Guid? institutionId,
        Guid? teacherUserId)
    {
        var scope = $"{normalizedEmail}|{role}|{institutionId?.ToString("N") ?? "-"}|{teacherUserId?.ToString("N") ?? "-"}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
