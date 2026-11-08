using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

// Roadmap O7: EngineVersion 2 applies the rule-based engine (WeeklyPlanEngine); 1 keeps the earlier behaviour for old clients.
// PrioritizeWhenShort lets a plan that does not fit be saved with exam-error and high-yield topics first.
// Paket H1 (08.11.2026): ReuseFreedSlots is on unless the request turns it off.
// Paket H4a: FocusTopicIds ("Önemli konulara odaklan", at most 50 active topics of the weekly lessons) go first within their lesson
// (engine version 2). Left out when null, so the fingerprint of a request without them stays as it was; an empty list counts as none.
public sealed record WeeklyOutcomePlanRequest(DateOnly StartDate,DateOnly EndDate,[property:JsonRequired] int ExpectedWeeklyDraftVersion,bool ReuseFreedSlots=true,
    int EngineVersion=1,bool PrioritizeWhenShort=false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<Guid>? FocusTopicIds=null);
public sealed record WeeklyExamDateRequest(DateOnly? ExamDate);
public sealed record WeeklyLeftOutTopic(Guid LessonId,string LessonName,Guid TopicId,string TopicName,int Minutes);
public sealed record WeeklyOutcomePlanSave([Required,MaxLength(200)] string Title,[Required] WeeklyOutcomePlanRequest Preview,[Required] string Fingerprint);
public sealed record WeeklyLessonIssue(Guid LessonId,string LessonName,string Message);
public sealed record WeeklyExamReference(string Name,DateOnly? Date);
// EndDateSource: Student (her own exam date), Session (the session she chose), Catalog (the admin's exam date), Estimated (G2: the year
// she chose has no session yet; the date is a guess) or Default (1 June). The plan ends the day before the exam. CatalogExamDate: the
// date her own date replaces.
public sealed record WeeklyOutcomePlanOptions(DateOnly StartDate,DateOnly EndDate,int WeeklyDraftVersion,string TimeZoneId,IReadOnlyList<WeeklyExamReference> Exams,
    DateOnly? ExamDate=null,string EndDateSource="Default",DateOnly? CatalogExamDate=null)
{
    /// <summary>G2 (#40): <see cref="CatalogExamDate"/> is an estimate (a year-only target).</summary>
    public bool CatalogExamDateEstimated { get; init; }
}
public sealed record WeeklyProtectedTask(DateOnly Date,int? StartMinute,string Title,int Minutes);
public sealed record WeeklyOutcomePlanPreview(WeeklyOutcomePlanRequest Request,string TimeZoneId,string Fingerprint,WeeklyOutcomeSchedule Schedule,IReadOnlyList<WeeklyLessonIssue> Issues,bool CanCreate,int ProtectedTaskCount,IReadOnlyList<WeeklyProtectedTask>? ProtectedTasks=null,
    IReadOnlyList<WeeklyLeftOutTopic>? LeftOutTopics=null,IReadOnlyList<WeeklyUnscheduledLesson>? UnscheduledLessons=null);
/// <summary>Paket G #41: a chosen course (Derslerim) with no lesson in the weekly draft; the yearly plan cannot include it.</summary>
public sealed record WeeklyUnscheduledLesson(Guid LessonId,string LessonName);
public sealed record WeeklyOutcomePlanCreated(Guid Id,int Version);
/// <summary>Paket H4a (H4b review): the error codes of a plan creation the client reads.</summary>
public static class WeeklyOutcomePlanCodes
{
    /// <summary>409: a plan draft is still open; it must be published or archived first.</summary>
    public const string DraftExists="StudyPlanning.DraftExists";
}
public interface IWeeklyOutcomePlanService
{
    Task<WeeklyOutcomePlanOptions> OptionsAsync(CancellationToken ct);
    Task<WeeklyOutcomePlanPreview> PreviewAsync(WeeklyOutcomePlanRequest request,CancellationToken ct);
    Task<WeeklyOutcomePlanCreated> CreateAsync(WeeklyOutcomePlanSave request,CancellationToken ct);
    Task<WeeklyOutcomePlanOptions> OptionsForStudentAsync(Guid studentId,CancellationToken ct);
    Task<WeeklyOutcomePlanPreview> PreviewForStudentAsync(Guid studentId,WeeklyOutcomePlanRequest request,CancellationToken ct);
    Task<WeeklyOutcomePlanCreated> CreateForStudentAsync(Guid studentId,WeeklyOutcomePlanSave request,CancellationToken ct);
    /// <summary>Roadmap O7: the student's own exam date (null returns to the admin's date).</summary>
    Task<WeeklyOutcomePlanOptions> SetExamDateAsync(WeeklyExamDateRequest request,CancellationToken ct);
}
