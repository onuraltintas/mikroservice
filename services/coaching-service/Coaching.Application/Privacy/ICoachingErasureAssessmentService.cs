using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace Coaching.Application.Privacy;

public interface ICoachingErasureAssessmentService
{
    Task<CoachingErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken);
}
