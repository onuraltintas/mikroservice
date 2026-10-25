using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace Identity.Domain.Entities;

public sealed class DataSubjectRequest : AggregateRoot
{
    public Guid RequesterUserId { get; private set; }
    public DataSubjectRequestType RequestType { get; private set; }
    public PersonalDataScope Scope { get; private set; }
    public DataSubjectRequestStatus Status { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime SubmittedAt { get; private set; }
    public DateTime? IdentityVerifiedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private DataSubjectRequest() { }

    public static DataSubjectRequest Create(
        Guid requesterUserId,
        DataSubjectRequestType requestType,
        PersonalDataScope scope,
        string reason,
        DateTime submittedAt)
    {
        if (requesterUserId == Guid.Empty)
            throw new ArgumentException("Requester is required.", nameof(requesterUserId));
        if (!Enum.IsDefined(requestType))
            throw new ArgumentOutOfRangeException(nameof(requestType));
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2_000)
            throw new ArgumentException("Reason is required and must not exceed 2000 characters.", nameof(reason));
        EnsureUtc(submittedAt, nameof(submittedAt));

        return new DataSubjectRequest
        {
            RequesterUserId = requesterUserId,
            RequestType = requestType,
            Scope = scope,
            Reason = reason.Trim(),
            Status = DataSubjectRequestStatus.Submitted,
            SubmittedAt = submittedAt,
            CreatedAt = submittedAt
        };
    }

    public static DataSubjectRequest Create(
        Guid requesterUserId,
        DataSubjectRequestType requestType,
        string reason,
        DateTime submittedAt) =>
        Create(requesterUserId, requestType, PersonalDataScope.Account, reason, submittedAt);

    public void VerifyIdentity(DateTime verifiedAt)
    {
        RequireStatus(DataSubjectRequestStatus.Submitted);
        EnsureAfterSubmission(verifiedAt, nameof(verifiedAt));
        IdentityVerifiedAt = verifiedAt;
        Status = DataSubjectRequestStatus.IdentityVerified;
        UpdatedAt = verifiedAt;
    }

    public void Approve(Guid decidedByUserId, string decisionReason, DateTime decidedAt) =>
        Decide(DataSubjectRequestStatus.Approved, decidedByUserId, decisionReason, decidedAt);

    public void Reject(Guid decidedByUserId, string decisionReason, DateTime decidedAt) =>
        Decide(DataSubjectRequestStatus.Rejected, decidedByUserId, decisionReason, decidedAt);

    public void StartProcessing(DateTime startedAt)
    {
        RequireStatus(DataSubjectRequestStatus.Approved);
        EnsureAfterSubmission(startedAt, nameof(startedAt));
        Status = DataSubjectRequestStatus.Processing;
        ProcessingStartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    public void Complete(DateTime completedAt)
    {
        RequireStatus(DataSubjectRequestStatus.Processing);
        EnsureAfterSubmission(completedAt, nameof(completedAt));
        Status = DataSubjectRequestStatus.Completed;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void Fail(string failureReason, DateTime failedAt)
    {
        RequireStatus(DataSubjectRequestStatus.Processing);
        if (string.IsNullOrWhiteSpace(failureReason) || failureReason.Trim().Length > 2_000)
            throw new ArgumentException("Failure reason is required and must not exceed 2000 characters.", nameof(failureReason));
        EnsureAfterSubmission(failedAt, nameof(failedAt));
        Status = DataSubjectRequestStatus.Failed;
        FailureReason = failureReason.Trim();
        UpdatedAt = failedAt;
    }

    private void Decide(
        DataSubjectRequestStatus status,
        Guid decidedByUserId,
        string decisionReason,
        DateTime decidedAt)
    {
        RequireStatus(DataSubjectRequestStatus.IdentityVerified);
        if (decidedByUserId == Guid.Empty)
            throw new ArgumentException("Decision maker is required.", nameof(decidedByUserId));
        if (string.IsNullOrWhiteSpace(decisionReason) || decisionReason.Trim().Length > 2_000)
            throw new ArgumentException("Decision reason is required and must not exceed 2000 characters.", nameof(decisionReason));
        EnsureAfterSubmission(decidedAt, nameof(decidedAt));
        Status = status;
        DecidedByUserId = decidedByUserId;
        DecisionReason = decisionReason.Trim();
        DecidedAt = decidedAt;
        UpdatedAt = decidedAt;
    }

    private void RequireStatus(DataSubjectRequestStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Request must be in {expected} state.");
    }

    private void EnsureAfterSubmission(DateTime value, string parameterName)
    {
        EnsureUtc(value, parameterName);
        if (value < SubmittedAt)
            throw new ArgumentOutOfRangeException(parameterName, "Timestamp cannot precede submission.");
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
