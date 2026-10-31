using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;

namespace Coaching.Infrastructure.Management;

public sealed record ParentChildOverdueItem(string Title, DateOnly Date, string Kind);
public sealed record ParentChildNextSession(DateTime StartsAt, string Title, string? TeacherName);
public sealed record ParentChildExamCountdown(string Name, DateOnly Date, int DaysLeft);
public sealed record ParentChildAttentionItem(string Kind, string Text, string? Link = null);
public sealed record ParentChildWeek(DateOnly WeekStart, int Planned, int Completed, IReadOnlyList<ParentChildOverdueItem> Overdue,
    ParentChildNextSession? NextSession, ParentChildExamCountdown? ExamCountdown, IReadOnlyList<ParentChildAttentionItem> Attention);
public sealed record ParentChildAdherenceWeek(DateOnly WeekStart, int Planned, int Completed, long? Minutes);
public sealed record ParentChildTeacherNote(string TeacherName, string Note, DateTime ReviewedAt);
public sealed record ParentChildDecision(string Title, string Note, DateOnly DueOn, string Status, bool IsOverdue, DateTime? CompletedAt);
public sealed record ParentChildWeeklyReview(DateOnly WeekStart, int Planned, int Completed, long? Minutes, bool ReflectionWritten,
    IReadOnlyList<ParentChildTeacherNote> TeacherNotes, IReadOnlyList<ParentChildDecision> Decisions);

/// <summary>A parent's authorized read of one child; only <see cref="CoachingParentChildService.RequireAsync"/> creates it.</summary>
public sealed class ParentChildScope
{
    internal ParentChildScope(Guid studentId, string firstName) { StudentId = studentId; FirstName = firstName; }
    public Guid StudentId { get; }
    public string FirstName { get; }
}

public sealed class CoachingParentChildService(CoachingDbContext db, ICoachingAccessPolicy access, OwnedCoachingDirectory directory,
    ICoachingAccountDirectory accounts, CoachingStudentTodayService today, TimeProvider clock)
{
    public Task<ParentChildScope> RequireAsync(Guid studentId, CancellationToken ct) => throw new NotImplementedException();
    public Task<ParentChildWeek> GetWeekAsync(ParentChildScope child, IReadOnlyList<ParentChildOverdueItem> overdue, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<ParentChildAdherenceWeek>> GetAdherenceAsync(ParentChildScope child, int weeks, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<ParentChildWeeklyReview>> GetWeeklyReviewsAsync(ParentChildScope child, int weeks, CancellationToken ct) => throw new NotImplementedException();
}
