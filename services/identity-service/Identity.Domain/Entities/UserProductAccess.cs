using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class UserProductAccess : Entity
{
    public Guid UserId { get; private set; }
    public PlatformProduct Product { get; private set; }
    public UserProductAccessSource Source { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive { get; private set; }

    private UserProductAccess() { }

    private UserProductAccess(
        Guid userId,
        PlatformProduct product,
        UserProductAccessSource source,
        Guid? grantedByUserId,
        DateTimeOffset grantedAt) : base(Guid.NewGuid())
    {
        UserId = userId;
        Product = product;
        Source = source;
        GrantedByUserId = grantedByUserId;
        GrantedAt = grantedAt;
        IsActive = true;
    }

    internal static UserProductAccess Create(
        Guid userId,
        PlatformProduct product,
        UserProductAccessSource source,
        Guid? grantedByUserId,
        DateTimeOffset grantedAt)
    {
        Validate(userId, product, source, grantedByUserId, grantedAt);
        return new UserProductAccess(userId, product, source, grantedByUserId, grantedAt);
    }

    internal void Reactivate(
        UserProductAccessSource source,
        Guid? grantedByUserId,
        DateTimeOffset grantedAt)
    {
        Validate(UserId, Product, source, grantedByUserId, grantedAt);
        Source = source;
        GrantedByUserId = grantedByUserId;
        GrantedAt = grantedAt;
        RevokedAt = null;
        IsActive = true;
    }

    internal void Revoke(DateTimeOffset revokedAt)
    {
        EnsureUtc(revokedAt, nameof(revokedAt));
        if (!IsActive)
            return;
        if (revokedAt < GrantedAt)
            throw new ArgumentOutOfRangeException(nameof(revokedAt), "Revocation cannot precede the grant.");

        RevokedAt = revokedAt;
        IsActive = false;
    }

    private static void Validate(
        Guid userId,
        PlatformProduct product,
        UserProductAccessSource source,
        Guid? grantedByUserId,
        DateTimeOffset grantedAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (!Enum.IsDefined(product))
            throw new ArgumentOutOfRangeException(nameof(product));
        if (!Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (grantedByUserId == Guid.Empty)
            throw new ArgumentException("Grantor must be a valid user when specified.", nameof(grantedByUserId));
        EnsureUtc(grantedAt, nameof(grantedAt));
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
