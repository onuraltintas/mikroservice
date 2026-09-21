using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace Coaching.Application.Privacy;

public interface ICoachingErasureExecutionService
{
    Task<CoachingErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken);
}
