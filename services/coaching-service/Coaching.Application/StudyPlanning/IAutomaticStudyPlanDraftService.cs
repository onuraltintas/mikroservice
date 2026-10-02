using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record AutomaticStudyDraftRequest([Required] string Title,
    [Required] AutomaticStudyPreviewRequest Preview,
    [property: JsonRequired] Guid? ExpectedActiveRevisionId,
    [property: JsonRequired] int? ExpectedActiveRevisionVersion);

public interface IAutomaticStudyPlanDraftService
{
    Task<ManualStudyPlanView> CreateAutomaticDraftAsync(AutomaticStudyDraftRequest request,
        CancellationToken cancellationToken = default);
}
