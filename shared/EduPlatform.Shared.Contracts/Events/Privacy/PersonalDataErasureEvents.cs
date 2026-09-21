namespace EduPlatform.Shared.Contracts.Events.Privacy;

public sealed record PersonalDataErasureAssessmentRequestedV1(
    Guid EventId,
    Guid RequestId,
    Guid SubjectUserId,
    DateTime ApprovedAt,
    bool DryRun,
    string SchemaVersion = "1.0");
