using System.Text.Json.Serialization;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record PracticeTopicOption(Guid Id, Guid LessonId, string LessonName, string Name, string SectionCode);
public sealed record PracticeAnalysisView(Guid ExamId, string Title, DateTime ExamDate, string Source, string Fingerprint,
    PracticeExamInput Input, PracticeTopicAnalysis Analysis, bool CanEdit, IReadOnlyDictionary<string, string>? SectionNames = null);
public sealed record PracticeAnalysisSave([property: JsonRequired] string Fingerprint, PracticeTopicAnalysis Analysis);
public sealed record PracticeAdviceTopicView(PracticeTopicAdvice Evidence, string LessonName, string TopicName, bool AlreadyPlanned);
public sealed record PracticeAdviceView(string ExamCode, string Source, IReadOnlyList<Guid> ExamIds,
    IReadOnlyList<PracticeAdviceTopicView> Topics);
public sealed record PracticeAdaptationView(IReadOnlyList<PracticeAnalysisView> Exams, IReadOnlyList<PracticeAdviceView> Advice);
public sealed record PracticeReviewChoice(Guid TopicId, int Minutes);
public sealed record PracticeAdjustmentRequest(string ExamCode, string Source, DateOnly EndDate,
    bool AllowDeadlineExtension, IReadOnlyList<PracticeReviewChoice> Topics);
public sealed record PracticeReviewSlot(Guid TopicId, Guid LessonId, string LessonName, string TopicName,
    DateOnly Date, int StartMinute, int Minutes, string EvidenceKey);
public sealed record PracticeFinishImpact(string LessonName, DateOnly? PreviousFinish, DateOnly? ProposedFinish, int AddedMinutes);
public sealed record PracticeCalendarTask(Guid Id, string Title, Guid? TopicId, DateOnly PlannedDate, int? PlannedStartMinute,
    int PlannedMinutes, bool IsCompleted, bool IsPinned, Guid? WeeklyLessonId, string? LessonName, int? ActualMinutes, DateTime? CompletedAt);
public sealed record PracticeAdjustmentPreview(string Fingerprint, Guid PlanId, int ExpectedVersion, bool CanApply,
    bool TeacherApprovalRequired, DateOnly? PreviousDeadline, DateOnly ProposedDeadline, string TimeZoneId,
    IReadOnlyList<PracticeReviewSlot> Slots, IReadOnlyList<PracticeReviewChoice> Unscheduled,
    IReadOnlyList<PracticeFinishImpact> Impact, string Message, IReadOnlyList<PracticeCalendarTask>? ExistingTasks = null);
public sealed record PracticeAdjustmentApply([property: JsonRequired] string Fingerprint,
    PracticeAdjustmentRequest Preview, [property: JsonRequired] bool Confirmed);
