using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record StudyTopicSelection(Guid TopicId, int? RequiredMinutes, IReadOnlyList<StudyOutcomeSelection>? Outcomes = null);
public sealed record AutomaticStudyPreviewRequest(DateOnly StartDate, int Days,
    [property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedAvailabilityVersion,
    [Required] IReadOnlyList<StudyTopicSelection> Topics, int? ExpectedLearningContextVersion = null);
public sealed record ProtectedStudyTask(Guid TaskId, DateOnly PlannedDate, string Title,
    int PlannedMinutes, bool IsPinned, bool IsCompleted);
public sealed record AutomaticStudyPreview(int AvailabilityVersion, string TimeZoneId,
    Guid? ActiveRevisionId, int? ActiveRevisionVersion,
    IReadOnlyList<ProtectedStudyTask> ProtectedTasks, StudyDraftSchedule Schedule, IReadOnlyList<ResolvedStudyOutcome>? Outcomes = null, IReadOnlyList<StudyContinuationPreview>? Continuations = null,
    string? EvidenceFingerprint = null,int CompletedOutcomeMinutes = 0);

public interface IAutomaticStudyPlanPreviewService
{
    Task<AutomaticStudyPreview> PreviewAsync(AutomaticStudyPreviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StudyContinuationPreview(Guid TaskId, DateOnly PlannedDate, int? StartMinute, int Minutes);
