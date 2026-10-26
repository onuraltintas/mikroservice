using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class UserProductAccess : Entity
{
    public Guid UserId { get; private set; }
    public PlatformProduct Product { get; private set; }
    public ProductAccessStatus Status { get; private set; }
    public ProductAccessSource Source { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => Status == ProductAccessStatus.Active;

    private UserProductAccess() { }

    public static UserProductAccess Grant(
        Guid userId,
        PlatformProduct product,
        ProductAccessSource source,
        Guid? grantedByUserId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user ID is required.", nameof(userId));

        if (!Enum.IsDefined(product))
            throw new ArgumentOutOfRangeException(nameof(product));

        if (!Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(source));

        return new UserProductAccess
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Product = product,
            Status = ProductAccessStatus.Active,
            Source = source,
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Suspend()
    {
        if (Status == ProductAccessStatus.Active)
            Status = ProductAccessStatus.Suspended;
    }

    public void Activate()
    {
        if (Status == ProductAccessStatus.Suspended)
            Status = ProductAccessStatus.Active;
    }

    public void Revoke()
    {
        if (Status == ProductAccessStatus.Revoked)
            return;

        Status = ProductAccessStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}
