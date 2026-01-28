using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Refresh Token for maintaining user sessions securely.
/// </summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReasonRevoked { get; private set; }
    
    // Computed Properties
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { } // EF Core

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt, string? createdByIp)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = createdByIp
        };
    }

    public void Revoke(string? revokedByIp, string reason)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReasonRevoked = reason;
    }
}
