using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklyReflectionSave(int? ExpectedVersion, string Successes, string? Obstacles, string NextFocus, int? Rating, string? ExpectedResultsFingerprint = null, IReadOnlyList<WeeklyDecisionEffect>? DecisionEffects = null);
public sealed record WeeklyReflectionView(int Version, string Successes, string Obstacles, string NextFocus, int? Rating, DateTime SavedAt, WeeklyResultSnapshot? Results = null, IReadOnlyList<WeeklyReflectionHistoryView>? History = null);
public sealed record WeeklyDecisionSave(int? ExpectedVersion, string Title, string? Note, DateOnly DueOn, string Status, int? SourceReflectionVersion = null, Guid? PreviousDecisionId = null, string? ExpectedResultsFingerprint = null);
public sealed record WeeklyDecisionPostpone([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [property:JsonRequired] DateOnly DueOn,[Required,StringLength(1000,MinimumLength=5)] string Reason);
public sealed record WeeklyDecisionView(Guid Id, int Version, DateOnly ReviewWeekStart, string Title, string Note, DateOnly DueOn,
    string Status, DateTime? CompletedAt, bool IsOverdue, bool CanEdit, IReadOnlyList<Coaching.Domain.Entities.WeeklyDecisionChange> History,
    WeeklyDecisionWork? Work = null, Guid? CreatedByTeacherId = null, Guid? ResponsibleTeacherId = null, DateOnly? InitialDueOn = null, WeeklyDecisionSourceView? SourceReflection = null, Guid? PreviousDecisionId = null, WeeklyResultDecision? PreviousDecision = null);
public sealed record WeeklyDecisionSourceView(Guid ReflectionId, int ReflectionVersion, DateOnly WeekStart,
    string Successes, string Obstacles, string NextFocus, int? Rating, WeeklyResultSnapshot? Results, WeeklyResultSnapshot? CurrentResults = null);
public sealed record WeeklyWorkSummary(int ScheduledTasks, int CompletedTasks, long PlannedMinutes, long? ActualMinutes, long? LegacyDateActualMinutes = null);
public sealed record WeeklyReviewView(DateOnly WeekStart, DateOnly Today, WeeklyReflectionView? Reflection, WeeklyWorkSummary Summary,
    IReadOnlyList<WeeklyDecisionView> Decisions, int TotalDecisions, int DecisionPage, bool CanReflect, bool CanDecide, IReadOnlyList<WeeklyReflectionReviewView>? Reviews = null, bool NeedsTeacherReview = false, WeeklyResultSnapshot? Results = null, string? ReviewReason = null);
public sealed record WeeklyHomeDecision(Guid Id, DateOnly WeekStart, string Title, DateOnly DueOn, bool IsOverdue);
public sealed record WeeklyHomeView(DateOnly WeekStart, DateOnly CurrentWeekStart, bool NeedsReflection, int OpenDecisions,
    int OverdueDecisions, IReadOnlyList<WeeklyHomeDecision> Decisions, int MissingReflectionCount = 0);

public sealed record WeeklyReflectionReviewSave(int ExpectedReflectionVersion, string Note, string? ExpectedResultsFingerprint = null);
public sealed record WeeklyReflectionReviewView(Guid TeacherId, int ReflectionVersion, string Note, DateTime ReviewedAt, string? EvidenceFingerprint = null, WeeklyResultSnapshot? Results = null, Guid? Id = null);

public sealed record WeeklyDecisionEffect(Guid DecisionId, string Effect, string Note);
public sealed record WeeklyResultStudy(Guid TaskId, Guid RootTaskId, Guid PlanId, int Version, DateOnly PlannedDate,
    string Title, int PlannedMinutes, int? ActualMinutes, int? ActualQuestions, bool IsCompleted, DateTime? CompletedAt, IReadOnlyList<Coaching.Domain.Entities.StudyWorkDay>? WorkDays = null, long? ResultRevision = null);
public sealed record WeeklyResultDecision(Guid DecisionId, int Version, DateOnly SourceWeekStart, string Title, string Status,
    DateOnly DueOn, DateTime? CompletedAt, WeeklyDecisionWork? Work, Guid? LinkedStudyRootId,
    IReadOnlyList<WeeklyResultStudy> RelatedStudy, string? Effect = null, string? EffectNote = null);
public sealed record WeeklyResultSnapshot(string Fingerprint, DateTime CapturedAt, WeeklyWorkSummary Summary,
    IReadOnlyList<WeeklyResultDecision> Decisions, IReadOnlyList<WeeklyResultStudy> Studies);

public sealed record WeeklyReflectionHistoryView(int Version, DateTime SavedAt, string Successes, string Obstacles,
    string NextFocus, int? Rating, WeeklyResultSnapshot? Results);
