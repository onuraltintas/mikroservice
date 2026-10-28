namespace Identity.Domain.Entities;

/// <summary>
/// A one-time code a student gives to a parent. The parent uses it to request a link, which the student then
/// approves. Only a hash of the code is stored; a new code replaces the student's unused ones.
/// </summary>
public sealed class ParentLinkCode
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid StudentUserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public Guid? UsedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private ParentLinkCode() { }

    public static ParentLinkCode Issue(Guid studentUserId, string codeHash, DateTime now)
    {
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        if (codeHash.Length != 64) throw new ArgumentException("A SHA-256 hex hash is required.", nameof(codeHash));
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(now));
        return new ParentLinkCode { Id = Guid.NewGuid(), StudentUserId = studentUserId, CodeHash = codeHash, CreatedAt = now, ExpiresAt = now + Lifetime };
    }

    public bool IsUsable(DateTime now) => UsedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Use(Guid parentUserId, DateTime now)
    {
        if (!IsUsable(now)) throw new InvalidOperationException("The code cannot be used.");
        UsedAt = now;
        UsedByUserId = parentUserId;
    }

    public void Revoke(DateTime now)
    {
        if (UsedAt is null && RevokedAt is null) RevokedAt = now;
    }
}
