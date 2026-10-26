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
public sealed class CoachingErasureBlockedException(string reasonCode) : InvalidOperationException(Describe(reasonCode))
{
    public const string LegalHold = "LegalHold";
    public const string AssessmentMissing = "AssessmentMissing";
    public const string AssessmentDenied = "AssessmentDenied";

    public string ReasonCode { get; } = reasonCode;

    private static string Describe(string reasonCode) => reasonCode switch
    {
        LegalHold => "Coaching erasure is blocked by an active legal hold.",
        AssessmentMissing => "A completed Coaching erasure assessment is required.",
        AssessmentDenied => "The Coaching erasure assessment does not authorize deletion.",
        _ => $"Coaching erasure is blocked: {reasonCode}."
    };
}
