namespace EduPlatform.Shared.Contracts.Events.Privacy;

public enum PersonalDataScope
{
    Account = 1,
    Coaching = 2,
    SpeedReading = 3
}

public sealed record PersonalDataErasureAssessmentRequestedV1(
    Guid EventId,
    Guid RequestId,
    Guid SubjectUserId,
    DateTime ApprovedAt,
    bool DryRun,
    PersonalDataScope Scope = PersonalDataScope.Account,
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
    IReadOnlyDictionary<string, int>? RecordCounts = null,
    string SchemaVersion = "1.0");
