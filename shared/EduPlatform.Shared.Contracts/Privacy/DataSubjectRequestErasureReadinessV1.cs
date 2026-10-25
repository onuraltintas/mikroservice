namespace EduPlatform.Shared.Contracts.Privacy;

public sealed record DataSubjectRequestErasureReadinessV1(
    Guid RequestId,
    Guid SubjectUserId,
    string RequestType,
    string Scope,
    string Status,
    bool AssessmentIsComplete,
    bool HasBlockingLegalHold,
    bool IsReadyForErasure);
