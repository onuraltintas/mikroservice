namespace EduPlatform.Shared.Contracts.Events.Privacy;

public sealed record PersonalDataErasureAssessmentRequestedV1(
    Guid EventId,
    Guid RequestId,
    Guid SubjectUserId,
    DateTime ApprovedAt,
    bool DryRun,
    string SchemaVersion = "1.0");

public sealed record PersonalDataErasureAssessmentCompletedV1(
    Guid EventId,
    Guid RequestId,
    Guid SubjectUserId,
    string ServiceName,
    bool CanProceed,
    bool HasActiveLegalHold,
    int AssignmentCount,
    int AttachmentCount,
    int ExamResultCount,
    int GoalCount,
    int SessionCount,
    int AgreementCount,
    DateTime AssessedAt,
    string SchemaVersion = "1.0");
