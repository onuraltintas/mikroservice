namespace CourseManagement.Domain;

/// <summary>Parts of an institution's data a support grant opens.</summary>
[Flags]
public enum SupportAccessSections
{
    None = 0,
    Students = 1,
    Schedule = 2,
    Finance = 4,
    Settings = 8
}

public enum SupportAccessStatus
{
    Requested = 1,
    Active = 2,
    Ended = 3
}

/// <summary>
/// Time-limited support access (19.11.2026). The platform does not see an institution's records (the institution is the
/// data controller); a named administrator may, only after the owner approves a request, for the sections and the time
/// the owner chose (at most seven days), read-only. The owner can end it at any moment; every view is logged.
/// </summary>
public sealed class SupportAccessGrant
{
    public static readonly int[] AllowedHours = [1, 24, 72, 168];
    public const int MaxReasonLength = 500;

    private SupportAccessGrant() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public SupportAccessStatus Status { get; private set; }
    public SupportAccessSections Sections { get; private set; }
    public int DurationHours { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string? EndReason { get; private set; }

    public static SupportAccessGrant Request(Guid workspaceId, Guid adminUserId, string reason, DateTimeOffset now)
    {
        if (workspaceId == Guid.Empty || adminUserId == Guid.Empty) throw new ArgumentException("Workspace and administrator are required.");
        var text = reason?.Trim() ?? string.Empty;
        if (text.Length is < 10 or > MaxReasonLength)
            throw new ArgumentException($"A reason of 10–{MaxReasonLength} characters is required.", nameof(reason));
        return new SupportAccessGrant
        {
            Id = Guid.NewGuid(), WorkspaceId = workspaceId, AdminUserId = adminUserId, Reason = text,
            Status = SupportAccessStatus.Requested, RequestedAt = now.ToUniversalTime()
        };
    }

    public bool IsActive(DateTimeOffset now) => Status == SupportAccessStatus.Active && ExpiresAt > now;

    public bool Opens(SupportAccessSections section, Guid adminUserId, DateTimeOffset now) =>
        IsActive(now) && adminUserId == AdminUserId && section != SupportAccessSections.None && Sections.HasFlag(section);

    public void Approve(Guid ownerUserId, int hours, SupportAccessSections sections, DateTimeOffset now)
    {
        if (Status != SupportAccessStatus.Requested) throw new InvalidOperationException("Only a request can be approved.");
        if (!AllowedHours.Contains(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
        const SupportAccessSections all = SupportAccessSections.Students | SupportAccessSections.Schedule
            | SupportAccessSections.Finance | SupportAccessSections.Settings;
        if (sections == SupportAccessSections.None || (sections & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(sections));
        Status = SupportAccessStatus.Active;
        Sections = sections;
        DurationHours = hours;
        ApprovedAt = now.ToUniversalTime();
        DecidedByUserId = ownerUserId;
        ExpiresAt = ApprovedAt.Value.AddHours(hours);
    }

    public void Decline(Guid ownerUserId, DateTimeOffset now)
    {
        if (Status != SupportAccessStatus.Requested) throw new InvalidOperationException("Only a request can be declined.");
        Status = SupportAccessStatus.Ended;
        DecidedByUserId = ownerUserId;
        EndedAt = now.ToUniversalTime();
        EndReason = "Declined";
    }

    /// <summary>The owner (or the administrator) ends it early. Ending an expired grant only records the time.</summary>
    public void End(string reason, DateTimeOffset now)
    {
        if (Status == SupportAccessStatus.Ended) return;
        var expired = Status == SupportAccessStatus.Active && ExpiresAt <= now;
        Status = SupportAccessStatus.Ended;
        EndedAt = expired ? ExpiresAt : now.ToUniversalTime();
        EndReason = expired ? "Expired" : reason;
    }
}

/// <summary>One look at an institution's data under a support grant; the owner sees these.</summary>
public sealed class SupportAccessLog
{
    private SupportAccessLog() { }

    public Guid Id { get; private set; }
    public Guid GrantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public SupportAccessSections Section { get; private set; }
    public string What { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static SupportAccessLog Create(SupportAccessGrant grant, SupportAccessSections section, string what, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), GrantId = grant.Id, WorkspaceId = grant.WorkspaceId, AdminUserId = grant.AdminUserId,
        Section = section, What = what.Length > 200 ? what[..200] : what, OccurredAt = now.ToUniversalTime()
    };
}
