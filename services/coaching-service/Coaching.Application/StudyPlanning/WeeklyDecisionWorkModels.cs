using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklyDecisionWork(DecisionLinkedWork? Study, DecisionLinkedWork? Assignment);
public sealed record DecisionLinkedWork(Guid Id, string Title, string Status, bool IsDone, bool IsOverdue, bool CanOpen,
    Guid? PlanId = null, DateOnly? Date = null, int? StartMinute = null, int? Minutes = null, int? ActualMinutes = null, decimal? Score = null, int? ActualQuestions = null, DateTime? CompletedAt = null, string? Feedback = null, DateTime? SubmittedAt = null, long? ResultRevision = null);
public sealed record DecisionLinksSave([property: JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [property: JsonRequired] Guid? StudyTaskId, [property: JsonRequired] Guid? AssignmentId);
public sealed record DecisionWorkPlan(Guid Id, int Version, DateOnly? StartDate, DateOnly? EndDate, string TimeZoneId);
public sealed record DecisionWorkOptions(IReadOnlyList<DecisionLinkedWork> Items, int TotalCount, int PageNumber, DecisionWorkPlan? Plan);
public sealed record DecisionStudyProposal([property: JsonRequired] int ExpectedDecisionVersion,
    [property: JsonRequired] int ExpectedPlanVersion, Guid PlanId, Guid? TaskId, DateOnly Date, int StartMinute, int Minutes);
public sealed record DecisionStudyPreview(DecisionStudyProposal Proposal, string Fingerprint, string Title, string TimeZoneId,
    DateOnly? PreviousDate, int? PreviousStartMinute, int OtherWorkCount, int DayMinutes, int UntimedWorkCount);
public sealed record DecisionStudyApply(DecisionStudyProposal Proposal, string Fingerprint,
    [property: JsonRequired] bool Confirmed, Guid RequestId);
public sealed record DecisionAssignmentCreate([property: JsonRequired] int ExpectedVersion, DateTime DueAt,
    Guid RequestId, [Range(1,1440)] int? EstimatedMinutes = null);
