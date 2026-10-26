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

public sealed record PersonalDataErasureExecutionRequestedV1(
    Guid EventId,
    Guid RequestId,
    Guid SubjectUserId,
    DateTime AuthorizedAt,
    PersonalDataScope Scope,
    string SchemaVersion = "1.0");

public sealed record PersonalDataErasureExecutionCompletedV1(
    Guid EventId,
    Guid RequestId,
    string ServiceName,
    int DeletedRecordCount,
    DateTime CompletedAt,
    string SchemaVersion = "1.0");

/// <summary>
/// A service refused to execute an approved erasure (for example an active legal hold or a missing
/// assessment). The request cannot complete until it is handled by an operator.
/// </summary>
public sealed record PersonalDataErasureExecutionBlockedV1(
    Guid EventId,
    Guid RequestId,
    string ServiceName,
    string ReasonCode,
    DateTime BlockedAt,
    string SchemaVersion = "1.0");
