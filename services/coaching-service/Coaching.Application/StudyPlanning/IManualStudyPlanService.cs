using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record ManualStudyTaskInput(DateOnly PlannedDate, [property: Required] string Title,
    int PlannedMinutes, Guid? TopicId, bool IsPinned);
public sealed record ManualStudyPlanInput([property: Required] string Title,
    [property: Required] IReadOnlyList<ManualStudyTaskInput> Tasks);
public sealed record ManualStudyPlanUpdate([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion, [property: Required] ManualStudyPlanInput Plan);
public sealed record StudyPlanPublishInput([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record StudyTaskCompleteInput([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion,
    [property: Range(1, 1440)] int ActualMinutes);
public sealed record StudyTaskRescheduleInput([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion,
    DateOnly PlannedDate);
public sealed record ManualStudyTaskView(Guid Id, DateOnly PlannedDate, string Title, int PlannedMinutes,
    Guid? TopicId, bool IsPinned, bool IsCompleted, int? ActualMinutes, DateTime? CompletedAt = null);
public sealed record ManualStudyPlanView(Guid Id, int Version, string Title, StudyPlanStatus Status,
    IReadOnlyList<ManualStudyTaskView> Tasks);
public sealed record StudyPlanSummary(Guid Id, int Version, string Title, StudyPlanStatus Status);
public sealed record StudyPlanPage(IReadOnlyList<StudyPlanSummary> Items, int TotalCount, int PageNumber, int PageSize);

public interface IManualStudyPlanService
{
    Task<StudyPlanPage> ListAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> CreateDraftAsync(ManualStudyPlanInput request, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> ReplaceDraftAsync(Guid id, int expectedVersion, ManualStudyPlanInput request, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> PublishAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> ArchiveDraftAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, CancellationToken cancellationToken = default);
    Task<ManualStudyPlanView> RescheduleTaskAsync(Guid id, Guid taskId, int expectedVersion, DateOnly plannedDate, CancellationToken cancellationToken = default);
}
