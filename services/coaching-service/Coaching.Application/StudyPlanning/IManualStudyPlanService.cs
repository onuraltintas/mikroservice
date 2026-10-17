using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record ManualStudyTaskInput(DateOnly PlannedDate, [Required] string Title,
    int PlannedMinutes, Guid? TopicId, bool IsPinned, [MaxLength(300)] string? SourceName = null,
    [Range(0, 100000)] int? PlannedQuestions = null, [Range(0,1439)] int? PlannedStartMinute = null);
public sealed record ManualStudyPlanInput([Required] string Title,
    [Required] IReadOnlyList<ManualStudyTaskInput> Tasks);
public sealed record ManualStudyPlanUpdate([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion, [Required] ManualStudyPlanInput Plan);
public sealed record StudyPlanPublishInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record StudyPlanRestoreInput([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [property:JsonRequired] Guid? ExpectedActiveRevisionId, [property:JsonRequired] [Range(0,int.MaxValue)] int? ExpectedActiveRevisionVersion);
public sealed record StudyTaskCompleteInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    [Range(1, 1440)] int ActualMinutes, [Range(0, 100000)] int? ActualQuestions = null);
public sealed record StudyTaskRescheduleInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    DateOnly PlannedDate);
public sealed record StudyTasksMoveInput([property: JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [Required] IReadOnlyList<Guid> TaskIds, DateOnly PlannedDate, [Range(0,1439)] int StartMinute);
public sealed record StudyTaskTitleInput([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,[Required,MaxLength(200)] string Title);
public sealed record ManualStudyTaskView(Guid Id, DateOnly PlannedDate, string Title, int PlannedMinutes,
    Guid? TopicId, bool IsPinned, bool IsCompleted, int? ActualMinutes, DateTime? CompletedAt = null,
    string? SourceName = null, int? PlannedQuestions = null, int? ActualQuestions = null,
    IReadOnlyList<StudyTaskOutcomeView>? Outcomes = null,int? PlannedStartMinute=null,Guid? WeeklyLessonId=null,string? LessonName=null);
public sealed record StudyTaskOutcomeView(Guid OutcomeId, string Code, string Name, Guid EstimateId, int EstimateNumber,
    int AllocatedMinutes, string Origin);
public sealed record ManualStudyPlanView(Guid Id, int Version, string Title, StudyPlanStatus Status,
    IReadOnlyList<ManualStudyTaskView> Tasks,WeeklyPlanSource? WeeklySource=null);
public sealed record StudyPlanSummary(Guid Id, int Version, string Title, StudyPlanStatus Status);
public sealed record StudyPlanPage(IReadOnlyList<StudyPlanSummary> Items, int TotalCount, int PageNumber, int PageSize);

public interface IManualStudyPlanService
{
    Task<StudyPlanPage> ListAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> CreateDraftAsync(ManualStudyPlanInput request, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> ReplaceDraftAsync(Guid id, int expectedVersion, ManualStudyPlanInput request, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> PublishAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> RestoreArchivedAsync(Guid id, StudyPlanRestoreInput request, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> ArchiveDraftAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, int? actualQuestions, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> RescheduleTaskAsync(Guid id, Guid taskId, int expectedVersion, DateOnly plannedDate, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> MoveTasksAsync(Guid id, StudyTasksMoveInput request, CancellationToken cancellationToken = default);
}
