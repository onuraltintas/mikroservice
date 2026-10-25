using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklyOutcomePlanRequest(DateOnly StartDate,DateOnly EndDate,[property:JsonRequired] int ExpectedWeeklyDraftVersion,bool ReuseFreedSlots=false);
public sealed record WeeklyOutcomePlanSave([Required,MaxLength(200)] string Title,[Required] WeeklyOutcomePlanRequest Preview,[Required] string Fingerprint);
public sealed record WeeklyLessonIssue(Guid LessonId,string LessonName,string Message);
public sealed record WeeklyExamReference(string Name,DateOnly? Date);
public sealed record WeeklyOutcomePlanOptions(DateOnly StartDate,DateOnly EndDate,int WeeklyDraftVersion,string TimeZoneId,IReadOnlyList<WeeklyExamReference> Exams);
public sealed record WeeklyProtectedTask(DateOnly Date,int? StartMinute,string Title,int Minutes);
public sealed record WeeklyOutcomePlanPreview(WeeklyOutcomePlanRequest Request,string TimeZoneId,string Fingerprint,WeeklyOutcomeSchedule Schedule,IReadOnlyList<WeeklyLessonIssue> Issues,bool CanCreate,int ProtectedTaskCount,IReadOnlyList<WeeklyProtectedTask>? ProtectedTasks=null);
public sealed record WeeklyOutcomePlanCreated(Guid Id,int Version);
public interface IWeeklyOutcomePlanService
{
    Task<WeeklyOutcomePlanOptions> OptionsAsync(CancellationToken ct);
    Task<WeeklyOutcomePlanPreview> PreviewAsync(WeeklyOutcomePlanRequest request,CancellationToken ct);
    Task<WeeklyOutcomePlanCreated> CreateAsync(WeeklyOutcomePlanSave request,CancellationToken ct);
    Task<WeeklyOutcomePlanOptions> OptionsForStudentAsync(Guid studentId,CancellationToken ct);
    Task<WeeklyOutcomePlanPreview> PreviewForStudentAsync(Guid studentId,WeeklyOutcomePlanRequest request,CancellationToken ct);
    Task<WeeklyOutcomePlanCreated> CreateForStudentAsync(Guid studentId,WeeklyOutcomePlanSave request,CancellationToken ct);
}
