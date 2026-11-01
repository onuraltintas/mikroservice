namespace Coaching.Application.Interfaces;

/// <summary>Verified parents (owned by Identity) of Coaching students.</summary>
public interface ICoachingParentDirectory
{
    /// <summary>Verified parents who want the weekly summary of their child (roadmap Y7).</summary>
    Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct);

    /// <summary>Roadmap O3: verified links of the given children, and of the given parent.</summary>
    Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct);
}

public sealed record CoachingParentLink(Guid ParentUserId, string ParentName, Guid StudentUserId, string StudentName, string Relationship);
public sealed record CoachingParentLinksRequest(IReadOnlyList<Guid> StudentUserIds, Guid? ParentUserId);

/// <summary>
/// V3 (parent panel): Identity also says which products the child uses and left visible to the parent. The defaults keep an
/// older Identity (without these fields) working as before: Coaching only.
/// </summary>
public sealed record CoachingParentSummaryRecipient(Guid ParentUserId, string ParentEmail, string ParentName, Guid StudentUserId, string StudentName,
    bool CoachingVisible = true, bool SpeedReadingVisible = true, bool StudentHasCoaching = true, bool StudentHasSpeedReading = false)
{
    public bool WantsCoaching => StudentHasCoaching && CoachingVisible;
    public bool WantsSpeedReading => StudentHasSpeedReading && SpeedReadingVisible;
}
public sealed record CoachingParentSummaryRecipientPage(IReadOnlyList<CoachingParentSummaryRecipient> Items, int TotalCount);

/// <summary>V3 (parent panel): the child's speed-reading week, read from the speed-reading service for the weekly parent e-mail.</summary>
public interface ISpeedReadingParentSummaryClient
{
    /// <summary>Null when the child has no speed-reading activity or profile at all (204), or the service is not configured.</summary>
    Task<SpeedReadingParentWeek?> GetWeekAsync(Guid studentUserId, DateOnly weekStart, CancellationToken ct);
}

public sealed record SpeedReadingParentWeek(int Minutes, int ActiveDays, int ExercisesCompleted, decimal? LatestWpm, decimal? WpmChange,
    decimal? AverageComprehension, int? ProgramDay, int? ProgramTotalDays, int BadgesEarnedThisWeek);
