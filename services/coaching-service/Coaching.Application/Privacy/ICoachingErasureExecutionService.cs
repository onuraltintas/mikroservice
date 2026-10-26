using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace Coaching.Application.Privacy;

public interface ICoachingErasureExecutionService
{
    Task<CoachingErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken);
}

/// <summary>The erasure was refused; the reason code is sent back to Identity so the request fails visibly.</summary>
public sealed class CoachingErasureBlockedException(string reasonCode) : InvalidOperationException($"Coaching erasure is blocked: {reasonCode}.")
{
    public const string LegalHold = "LegalHold";
    public const string AssessmentMissing = "AssessmentMissing";
    public const string AssessmentDenied = "AssessmentDenied";

    public string ReasonCode { get; } = reasonCode;
}
