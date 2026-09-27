using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Institutions;

public enum SpeedReadingInstitutionMemberRole
{
    Student = 1,
    Teacher = 2
}

/// <summary>
/// Institution membership owned by the Speed Reading bounded context.
/// InstitutionId is a reference to the shared institution directory and is
/// intentionally not a cross-database foreign key.
/// </summary>
public sealed class SpeedReadingInstitutionMembership : AggregateRoot
{
    private SpeedReadingInstitutionMembership()
    {
    }

    public Guid InstitutionId { get; private set; }
    public Guid UserId { get; private set; }
    public SpeedReadingInstitutionMemberRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static SpeedReadingInstitutionMembership Create(
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role,
        Guid actorId,
        DateTime at)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution is required.", nameof(institutionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("Member user is required.", nameof(userId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Membership actor is required.", nameof(actorId));
        EnsureRole(role);

        var membership = new SpeedReadingInstitutionMembership
        {
            InstitutionId = institutionId,
            UserId = userId,
            Role = role,
            IsActive = true,
            CreatedAt = EnsureUtc(at),
            CreatedBy = actorId.ToString()
        };
        return membership;
    }

    public void ChangeRole(SpeedReadingInstitutionMemberRole role, Guid actorId, DateTime at)
    {
        EnsureRole(role);
        UpdateAudit(actorId, at);
        Role = role;
    }

    public void SetActive(bool isActive, Guid actorId, DateTime at)
    {
        UpdateAudit(actorId, at);
        IsActive = isActive;
    }

    private void UpdateAudit(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Membership actor is required.", nameof(actorId));

        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static void EnsureRole(SpeedReadingInstitutionMemberRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
