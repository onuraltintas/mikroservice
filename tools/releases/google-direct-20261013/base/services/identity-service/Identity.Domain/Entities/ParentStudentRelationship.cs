using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Auditable link between one parent or legal representative and one student.
/// The legacy StudentProfile.ParentId is not treated as verification evidence.
/// </summary>
public sealed class ParentStudentRelationship : AggregateRoot
{
    public Guid ParentUserId { get; private set; }
    public User ParentUser { get; private set; } = null!;
    public Guid StudentUserId { get; private set; }
    public User StudentUser { get; private set; } = null!;
    public ParentRelationship Relationship { get; private set; }
    public ParentStudentRelationshipStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public ParentStudentVerificationMethod? VerificationMethod { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    private ParentStudentRelationship() { }

    public static ParentStudentRelationship Request(
        Guid parentUserId,
        Guid studentUserId,
        ParentRelationship relationship,
        Guid requestedByUserId,
        DateTime requestedAt)
    {
        EnsureUserId(parentUserId, nameof(parentUserId));
        EnsureUserId(studentUserId, nameof(studentUserId));
        EnsureUserId(requestedByUserId, nameof(requestedByUserId));
        if (parentUserId == studentUserId)
            throw new ArgumentException("Parent and student must be different users.", nameof(studentUserId));
        if (!Enum.IsDefined(relationship))
            throw new ArgumentOutOfRangeException(nameof(relationship));
        EnsureUtc(requestedAt, nameof(requestedAt));

        return new ParentStudentRelationship
        {
            ParentUserId = parentUserId,
            StudentUserId = studentUserId,
            Relationship = relationship,
            Status = ParentStudentRelationshipStatus.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAt = requestedAt,
            CreatedAt = requestedAt
        };
    }

    public void Verify(
        ParentStudentVerificationMethod method,
        Guid verifiedByUserId,
        DateTime verifiedAt)
    {
        if (Status != ParentStudentRelationshipStatus.Pending)
            throw new InvalidOperationException("Only a pending parent-student relationship can be verified.");
        if (!Enum.IsDefined(method))
            throw new ArgumentOutOfRangeException(nameof(method));
        EnsureUserId(verifiedByUserId, nameof(verifiedByUserId));
        EnsureUtc(verifiedAt, nameof(verifiedAt));
        if (verifiedAt < RequestedAt)
            throw new ArgumentOutOfRangeException(nameof(verifiedAt));

        Status = ParentStudentRelationshipStatus.Verified;
        VerificationMethod = method;
        VerifiedByUserId = verifiedByUserId;
        VerifiedAt = verifiedAt;
        UpdatedAt = verifiedAt;
    }

    public void Revoke(Guid revokedByUserId, DateTime revokedAt, string reason)
    {
        if (Status == ParentStudentRelationshipStatus.Revoked)
            throw new InvalidOperationException("Parent-student relationship is already revoked.");
        EnsureUserId(revokedByUserId, nameof(revokedByUserId));
        EnsureUtc(revokedAt, nameof(revokedAt));
        if (revokedAt < RequestedAt)
            throw new ArgumentOutOfRangeException(nameof(revokedAt));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Revocation reason is required and must not exceed 500 characters.", nameof(reason));

        Status = ParentStudentRelationshipStatus.Revoked;
        RevokedByUserId = revokedByUserId;
        RevokedAt = revokedAt;
        RevocationReason = reason.Trim();
        UpdatedAt = revokedAt;
    }

    private static void EnsureUserId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("User is required.", parameterName);
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
