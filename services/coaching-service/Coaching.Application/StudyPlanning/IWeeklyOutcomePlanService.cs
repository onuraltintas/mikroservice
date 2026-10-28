using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

// Roadmap O7: EngineVersion 2 applies the rule-based engine (WeeklyPlanEngine); 1 keeps the earlier behaviour for old clients.
// PrioritizeWhenShort lets a plan that does not fit be saved with exam-error and high-yield topics first.
public sealed record WeeklyOutcomePlanRequest(DateOnly StartDate,DateOnly EndDate,[property:JsonRequired] int ExpectedWeeklyDraftVersion,bool ReuseFreedSlots=false,
    int EngineVersion=1,bool PrioritizeWhenShort=false);
public sealed record WeeklyExamDateRequest(DateOnly? ExamDate);
public sealed record WeeklyLeftOutTopic(Guid LessonId,string LessonName,Guid TopicId,string TopicName,int Minutes);
public sealed record WeeklyOutcomePlanSave([Required,MaxLength(200)] string Title,[Required] WeeklyOutcomePlanRequest Preview,[Required] string Fingerprint);
public sealed record WeeklyLessonIssue(Guid LessonId,string LessonName,string Message);
public sealed record WeeklyExamReference(string Name,DateOnly? Date);
// EndDateSource: Student (her own exam date), Catalog (the admin's exam date) or Default (1 June). The plan ends the day before the exam.
public sealed record WeeklyOutcomePlanOptions(DateOnly StartDate,DateOnly EndDate,int WeeklyDraftVersion,string TimeZoneId,IReadOnlyList<WeeklyExamReference> Exams,
    DateOnly? ExamDate=null,string EndDateSource="Default",DateOnly? CatalogExamDate=null);
public sealed record WeeklyProtectedTask(DateOnly Date,int? StartMinute,string Title,int Minutes);
public sealed record WeeklyOutcomePlanPreview(WeeklyOutcomePlanRequest Request,string TimeZoneId,string Fingerprint,WeeklyOutcomeSchedule Schedule,IReadOnlyList<WeeklyLessonIssue> Issues,bool CanCreate,int ProtectedTaskCount,IReadOnlyList<WeeklyProtectedTask>? ProtectedTasks=null,
    IReadOnlyList<WeeklyLeftOutTopic>? LeftOutTopics=null);
public sealed record WeeklyOutcomePlanCreated(Guid Id,int Version);
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
