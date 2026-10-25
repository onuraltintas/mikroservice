using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Refresh Token for maintaining user sessions securely.
/// </summary>
public class RefreshToken : Entity
{
    /// <summary>Revocation reason written when a token is exchanged for its successor.</summary>
    public const string RotatedReason = "Refreshed";

    public Guid UserId { get; private set; }

    /// <summary>
    /// SHA-256 digest (lower-case hex) of the token value. The value itself is never
    /// stored, so a database copy cannot be replayed as a session.
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// The value handed to the client. Only available on a token created in this
    /// process; tokens read from the database carry the digest alone.
    /// </summary>
    public string? IssuedValue { get; private set; }

    /// <summary>Every token rotated from the same sign-in shares the family id.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>When the sign-in that started the family happened; caps the session lifetime.</summary>
    public DateTime SessionStartedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReasonRevoked { get; private set; }
    public bool IsPersistent { get; private set; }
    public DateTimeOffset? MfaVerifiedAt { get; private set; }
    public PlatformProduct? Product { get; private set; }
    
    // Computed Properties
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { } // EF Core

    public static RefreshToken Create(
        Guid userId,
        string token,
        DateTime expiresAt,
        string? createdByIp,
        bool isPersistent = true,
        DateTimeOffset? mfaVerifiedAt = null,
        PlatformProduct? product = null,
        Guid? familyId = null,
        DateTime? sessionStartedAt = null)
    {
        if (product.HasValue && !Enum.IsDefined(product.Value))
            throw new ArgumentOutOfRangeException(nameof(product));
        ArgumentException.ThrowIfNullOrEmpty(token);

        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            Token = Digest(token),
            IssuedValue = token,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            CreatedByIp = createdByIp,
            IsPersistent = isPersistent,
            MfaVerifiedAt = mfaVerifiedAt,
            Product = product,
            FamilyId = familyId ?? id,
            SessionStartedAt = sessionStartedAt ?? createdAt
        };
    }

    /// <summary>Lower-case hex SHA-256 of a presented token value (the stored form).</summary>
    public static string Digest(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Constant-time check of a presented value against the stored digest.</summary>
    public bool Matches(string? presented) =>
        !string.IsNullOrEmpty(presented)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Digest(presented)),
            Encoding.ASCII.GetBytes(Token));

    /// <summary>
    /// Makes this freshly issued token the successor of <paramref name="previous"/>: it
    /// joins the same family and can never outlive the original sign-in by more than
    /// <paramref name="maxSessionLifetime"/>, however often it is rotated.
    /// </summary>
    public void ContinueSessionOf(RefreshToken previous, TimeSpan maxSessionLifetime)
    {
        ArgumentNullException.ThrowIfNull(previous);
        FamilyId = previous.FamilyId;
        SessionStartedAt = previous.SessionStartedAt;
        var absoluteExpiry = SessionStartedAt + maxSessionLifetime;
        if (ExpiresAt > absoluteExpiry)
        {
            ExpiresAt = absoluteExpiry;
        }
    }

    public void Revoke(string? revokedByIp, string reason)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReasonRevoked = reason;
    }
}
