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
/// the owner chose (at most seven days). Read-only unless the owner also allows corrections (<see cref="AllowsChanges"/>);
/// even then only a short list of corrections is possible, none of them tells students or guardians anything, and money
/// records stay the institution's. The owner can end it at any moment; every view and every change is logged.
/// The owner is e-mailed when support asks, when access opens and when it ends (the *NoticeSentAt fields).
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
    /// <summary>The owner also allowed corrections in the opened sections (19.11.2026).</summary>
    public bool AllowsChanges { get; private set; }
    public DateTimeOffset? RequestNoticeSentAt { get; private set; }
    public DateTimeOffset? StartNoticeSentAt { get; private set; }
    public DateTimeOffset? EndNoticeSentAt { get; private set; }

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

    public bool OpensForChange(SupportAccessSections section, Guid adminUserId, DateTimeOffset now) =>
        AllowsChanges && Opens(section, adminUserId, now);

    public void Approve(Guid ownerUserId, int hours, SupportAccessSections sections, DateTimeOffset now) =>
        Approve(ownerUserId, hours, sections, false, now);

    public void Approve(Guid ownerUserId, int hours, SupportAccessSections sections, bool allowChanges, DateTimeOffset now)
    {
        if (Status != SupportAccessStatus.Requested) throw new InvalidOperationException("Only a request can be approved.");
        if (!AllowedHours.Contains(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
        const SupportAccessSections all = SupportAccessSections.Students | SupportAccessSections.Schedule
            | SupportAccessSections.Finance | SupportAccessSections.Settings;
        if (sections == SupportAccessSections.None || (sections & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(sections));
        Status = SupportAccessStatus.Active;
        Sections = sections;
        AllowsChanges = allowChanges;
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

    /// <summary>Which owner e-mail is due now, if any: the request, the opening, or the end (also a quiet expiry).</summary>
    public SupportAccessNotice? DueNotice(DateTimeOffset now)
    {
        if (RequestNoticeSentAt is null && Status == SupportAccessStatus.Requested) return SupportAccessNotice.Requested;
        if (StartNoticeSentAt is null && Status == SupportAccessStatus.Active && IsActive(now)) return SupportAccessNotice.Started;
        if (StartNoticeSentAt is not null && EndNoticeSentAt is null
            && (Status == SupportAccessStatus.Ended || (Status == SupportAccessStatus.Active && !IsActive(now))))
            return SupportAccessNotice.Ended;
        return null;
    }

    public void MarkNoticeSent(SupportAccessNotice notice, DateTimeOffset now)
    {
        var at = now.ToUniversalTime();
        switch (notice)
        {
            case SupportAccessNotice.Requested: RequestNoticeSentAt ??= at; break;
            case SupportAccessNotice.Started: StartNoticeSentAt ??= at; RequestNoticeSentAt ??= at; break;
            case SupportAccessNotice.Ended: EndNoticeSentAt ??= at; break;
        }
    }
}

public enum SupportAccessNotice
{
    Requested = 1,
    Started = 2,
    Ended = 3
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
    /// <summary>A correction rather than a look.</summary>
    public bool IsChange { get; private set; }
    /// <summary>The record changed (student, guardian, lesson); its name is read when shown, so an erased name stays erased.</summary>
    public Guid? TargetId { get; private set; }
    /// <summary>Before and after, as codes or field names (no personal data): "Present", "10:00", "e-posta, telefon".</summary>
    public string? Before { get; private set; }
    public string? After { get; private set; }

    public static SupportAccessLog Create(SupportAccessGrant grant, SupportAccessSections section, string what, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), GrantId = grant.Id, WorkspaceId = grant.WorkspaceId, AdminUserId = grant.AdminUserId,
        Section = section, What = Cut(what, 200), OccurredAt = now.ToUniversalTime()
    };

    public static SupportAccessLog Change(SupportAccessGrant grant, SupportAccessSections section, string what, Guid targetId,
        string? before, string? after, DateTimeOffset now)
    {
        var log = Create(grant, section, what, now);
        log.IsChange = true;
        log.TargetId = targetId;
        log.Before = before is null ? null : Cut(before, 200);
        log.After = after is null ? null : Cut(after, 200);
        return log;
    }

    private static string Cut(string text, int length) => text.Length > length ? text[..length] : text;
}
