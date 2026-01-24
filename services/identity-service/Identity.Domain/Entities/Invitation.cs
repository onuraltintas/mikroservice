using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Davet (Invitation) - Kurum veya öğretmenin kullanıcıları kendilerine bağlamak için gönderdiği davetler
/// </summary>
public class Invitation : AggregateRoot
{
    public Guid InviterId { get; private set; } // Daveti gönderen (Kurum veya Öğretmen UserId)
    public string InviteeEmail { get; private set; } = string.Empty; // Davet edilen kişinin e-postası
    public Guid? InviteeUserId { get; private set; } // Davet edilen kişi sisteme kayıtlıysa UserId
    
    public InvitationType Type { get; private set; }
    public InvitationStatus Status { get; private set; }
    
    public Guid? InstitutionId { get; private set; } // Eğer kurum daveti ise InstitutionId
    public Guid? TeacherId { get; private set; } // Eğer öğretmen daveti ise TeacherId
    
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    
    public string? Message { get; private set; } // Davet mesajı (İsteğe bağlı)

    private Invitation() { } // EF Constructor

    public static Invitation Create(
        Guid inviterId,
        string inviteeEmail,
        InvitationType type,
        Guid? institutionId = null,
        Guid? teacherId = null,
        string? message = null,
        int expirationDays = 7)
    {
        var invitation = new Invitation
        {
            InviterId = inviterId,
            InviteeEmail = inviteeEmail.ToLowerInvariant(),
            Type = type,
            Status = InvitationStatus.Pending,
            InstitutionId = institutionId,
            TeacherId = teacherId,
            Message = message,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };

        return invitation;
    }

    public void Accept(Guid inviteeUserId)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be accepted");
            
        if (DateTime.UtcNow > ExpiresAt)
            throw new InvalidOperationException("Invitation has expired");

        Status = InvitationStatus.Accepted;
        InviteeUserId = inviteeUserId;
        RespondedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be rejected");

        Status = InvitationStatus.Rejected;
        RespondedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsExpired()
    {
        if (Status == InvitationStatus.Pending && DateTime.UtcNow > ExpiresAt)
        {
            Status = InvitationStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
    public bool IsPending() => Status == InvitationStatus.Pending && !IsExpired();
}
