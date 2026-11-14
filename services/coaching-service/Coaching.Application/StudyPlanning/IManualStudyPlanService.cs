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
public sealed record StudyRealizationInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    [Range(0,1440)] int ActualMinutes, [Range(0,100000)] int? ActualQuestions, bool IsCompleted, string? Reason = null, [Range(1,1440)] int? RemainingMinutes = null, [Range(0,100000)] int? RemainingQuestions = null, DateOnly? WorkedOn = null, DateOnly? ReassignFromDate = null,
    StudyContinuationScopeInput? ContinuationScope = null);
public sealed record StudyContinuationScopeInput(Guid RevisionId, [Range(0,int.MaxValue)] int ExpectedVersion,
    [Range(1,1440)] int RemainingMinutes, [Range(0,100000)] int? RemainingQuestions = null);
public sealed record StudyContinuationView(Guid RevisionId,int RevisionVersion,Guid TaskId,bool IsCompleted,bool HasOwnCompletion,
    int? RemainingMinutes,int? RemainingQuestions,bool RequiresExplicitScope);
public sealed record StudyRemainingScopeInput([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [Range(1,1440)] int RemainingMinutes, [Range(0,100000)] int? RemainingQuestions = null);
public sealed record StudyTaskRescheduleInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    DateOnly PlannedDate);
public sealed record StudyTasksMoveInput([property: JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [Required] IReadOnlyList<Guid> TaskIds, DateOnly PlannedDate, [Range(0,1439)] int StartMinute);
public sealed record StudyTaskTitleInput([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,[Required,MaxLength(200)] string Title);
/// <summary>
/// A task as a plan shows it. An empty field is left out of the JSON instead of being written as null: a year plan holds
/// thousands of tasks and every change to one answers with the whole plan (measured on live 14.11.2026: 4,496 tasks, 3.9 MB).
/// </summary>
public sealed record ManualStudyTaskView(Guid Id,
    DateOnly PlannedDate,
    string Title,
    int PlannedMinutes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? TopicId,
    bool IsPinned,
    bool IsCompleted,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActualMinutes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? CompletedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PlannedQuestions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActualQuestions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StudyTaskOutcomeView>? Outcomes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PlannedStartMinute=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? WeeklyLessonId=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LessonName=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StudyRealizationChange>? RealizationHistory=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RemainingMinutes=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RemainingQuestions=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OriginalTaskId=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? PredecessorTaskId=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StudyWorkDay>? WorkDays=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CompletionSourceTaskId=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StudyContinuationView? Continuation=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? SkippedAt=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PlanKind=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PlanReason=null);
public sealed record StudyTaskOutcomeView(Guid OutcomeId, string Code, string Name, Guid EstimateId, int EstimateNumber,
    int AllocatedMinutes, string Origin);
public sealed record ManualStudyPlanView(Guid Id, int Version, string Title, StudyPlanStatus Status,
    IReadOnlyList<ManualStudyTaskView> Tasks,WeeklyPlanSource? WeeklySource=null);
public sealed record StudyPlanSummary(Guid Id, int Version, string Title, StudyPlanStatus Status);
public sealed record StudyPlanPage(IReadOnlyList<StudyPlanSummary> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record CalendarTaskSaveInput([property:JsonRequired] [Range(0,int.MaxValue)] int ExpectedVersion,
    [Required,MaxLength(200)] string Title, DateOnly PlannedDate, [Range(0,1439)] int StartMinute,
    [Range(1,1440)] int PlannedMinutes, Guid? LessonId = null);

public interface IManualStudyPlanService
{
    Task<ManualStudyPlanView> SetRemainingScopeAsync(Guid id, Guid taskId, StudyRemainingScopeInput request, CancellationToken ct = default);
    Task<ManualStudyPlanView> RecordRealizationAsync(Guid id, Guid taskId, StudyRealizationInput request, bool correction, CancellationToken ct = default);
    Task<ManualStudyPlanView> SaveCalendarTaskAsync(Guid id, Guid? taskId, CalendarTaskSaveInput request, CancellationToken ct = default);
    Task<ManualStudyPlanView> DeleteCalendarTaskAsync(Guid id, Guid taskId, int expectedVersion, CancellationToken ct = default);
    /// <summary>Paket H5: splits a timed, untouched task of at least an hour into two consecutive halves.</summary>
    Task<ManualStudyPlanView> SplitCalendarTaskAsync(Guid id, Guid taskId, int expectedVersion, CancellationToken ct = default);
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
    Task<ManualStudyPlanView> SkipTaskAsync(Guid id, Guid taskId, int expectedVersion, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> MoveTasksAsync(Guid id, StudyTasksMoveInput request, CancellationToken cancellationToken = default);
}
