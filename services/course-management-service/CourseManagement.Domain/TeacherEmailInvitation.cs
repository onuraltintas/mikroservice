using System.Security.Cryptography;
using System.Text;

namespace CourseManagement.Domain;

/// <summary>
/// A teacher invited by e-mail address (maxkampus.com, 19.11.2026). The address need not have an account: the e-mail
/// carries a link with a one-time code (only its hash is kept). The teacher opens an account, or uses an existing one
/// with the same address, and accepts; the institution then sees them among its teachers.
/// </summary>
public sealed class TeacherEmailInvitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    private TeacherEmailInvitation() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public InvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AcceptedUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    /// <summary>The name the owner gave (setup guide); shown as "Ad (davet bekliyor)" until the teacher accepts.</summary>
    public string? DisplayName { get; private set; }

    public void SetDisplayName(string? displayName)
    {
        var name = displayName?.Trim();
        DisplayName = string.IsNullOrEmpty(name) ? null : name.Length > 200 ? name[..200] : name;
    }

    /// <summary>A new invitation and its code (shown once, in the e-mail link).</summary>
    public static (TeacherEmailInvitation Invitation, string Code) Create(Guid id, Guid workspaceId, string email, Guid createdBy, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || createdBy == Guid.Empty || string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Invitation identifiers and e-mail are required.");
        var invitation = new TeacherEmailInvitation { Id = id, WorkspaceId = workspaceId, Email = email.Trim().ToLowerInvariant(), CreatedByUserId = createdBy };
        return (invitation, invitation.Renew(now));
    }

    /// <summary>Sends the invitation again: a new code (the old link stops working) and a new expiry.</summary>
    public string Renew(DateTimeOffset now)
    {
        if (Status is InvitationStatus.Accepted) throw new InvalidOperationException("An accepted invitation is not renewed.");
        var code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        CodeHash = HashCode(code);
        Status = InvitationStatus.Pending;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
        return code;
    }

    public bool IsOpen(DateTimeOffset now) => Status == InvitationStatus.Pending && now < ExpiresAt;

    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (!IsOpen(now) || userId == Guid.Empty) throw new InvalidOperationException("Invitation is not available.");
        Status = InvitationStatus.Accepted;
        AcceptedUserId = userId;
        AcceptedAt = now;
    }

    public void Cancel()
    {
        if (Status != InvitationStatus.Pending) throw new InvalidOperationException("Only pending invitations can be cancelled.");
        Status = InvitationStatus.Cancelled;
    }

    public static string HashCode(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToLowerInvariant())));

    public static bool IsWellFormedCode(string? code) => code is { Length: 64 } && code.All(Uri.IsHexDigit);
}
