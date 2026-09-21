using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.CoachingAgreements;

/// <summary>
/// Marker for the agreement workflow itself. These requests must remain reachable
/// so a student can inspect and acknowledge the prerequisite that protects all
/// other coaching operations.
/// </summary>
public interface IBypassesCoachingAgreementRequirement;

/// <summary>
/// Enforces the current coaching agreement at the application boundary so direct
/// API calls cannot bypass the student UI gate.
/// </summary>
public sealed class CoachingAgreementRequirementBehavior<TRequest, TResponse>(
    ICoachingAgreementRepository repository,
    ICoachingAccessPolicy accessPolicy,
    TimeProvider timeProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IBypassesCoachingAgreementRequirement
            || accessPolicy.CurrentUserId is not { } userId
            || !accessPolicy.IsCurrentStudent(userId))
        {
            return await next();
        }

        var current = await repository.GetCurrentAsync(
            "tr-TR",
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (current is null)
        {
            throw new BusinessRuleException(
                "CoachingAgreement.Unavailable",
                "Yürürlükte bir koçluk anlaşması bulunamadı. Lütfen daha sonra tekrar deneyin.");
        }

        var acknowledgement = await repository.GetActiveAcknowledgementForStudentAsync(
            current.Id,
            userId,
            cancellationToken);
        if (acknowledgement is null)
        {
            throw new BusinessRuleException(
                "CoachingAgreement.Required",
                "Koçluk işlemine devam etmek için güncel koçluk anlaşmasını kabul etmeniz gerekir.");
        }

        return await next();
    }
}
