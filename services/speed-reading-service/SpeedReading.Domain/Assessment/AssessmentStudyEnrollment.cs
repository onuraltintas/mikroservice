using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assessment;

public sealed class AssessmentStudyEnrollment : AggregateRoot
{
    private AssessmentStudyEnrollment()
    {
    }

    public Guid StudentId { get; private set; }
    public Guid? StudyDefinitionId { get; private set; }
    public string StudyCode { get; private set; } = string.Empty;
    public string ProtocolVersion { get; private set; } = string.Empty;
    public string CohortCode { get; private set; } = string.Empty;
    public DateTime ConsentRecordedAt { get; private set; }
    public string? ConsentDocumentVersion { get; private set; }
    public string? ConsentDocumentReference { get; private set; }
    public DateTime EnrolledAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }

    public static AssessmentStudyEnrollment Create(
        Guid id,
        Guid studentId,
        Guid studyDefinitionId,
        string studyCode,
        string protocolVersion,
        string cohortCode,
        string consentDocumentVersion,
        string consentDocumentReference,
        DateTime consentRecordedAt,
        Guid actorId,
        DateTime enrolledAt)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || studyDefinitionId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Enrollment identifiers are required.");
        Validate(studyCode, protocolVersion, cohortCode, consentDocumentVersion, consentDocumentReference);
        if (EnsureUtc(consentRecordedAt) > EnsureUtc(enrolledAt))
            throw new ArgumentException("Consent cannot be recorded after enrollment.", nameof(consentRecordedAt));
        return new AssessmentStudyEnrollment
        {
            Id = id,
            StudentId = studentId,
            StudyDefinitionId = studyDefinitionId,
            StudyCode = studyCode.Trim(),
            ProtocolVersion = protocolVersion.Trim(),
            CohortCode = cohortCode.Trim(),
            ConsentRecordedAt = EnsureUtc(consentRecordedAt),
            ConsentDocumentVersion = consentDocumentVersion.Trim(),
            ConsentDocumentReference = consentDocumentReference.Trim(),
            EnrolledAt = EnsureUtc(enrolledAt),
            IsActive = true,
            CreatedAt = EnsureUtc(enrolledAt),
            CreatedBy = actorId.ToString()
        };
    }

    public void Withdraw(Guid actorId, DateTime withdrawnAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A valid actor is required.", nameof(actorId));
        if (!IsActive) return;
        IsActive = false;
        WithdrawnAt = EnsureUtc(withdrawnAt);
        UpdatedAt = WithdrawnAt;
        UpdatedBy = actorId.ToString();
    }

    private static void Validate(string studyCode, string protocolVersion, string cohortCode, string consentDocumentVersion, string consentDocumentReference)
    {
        if (string.IsNullOrWhiteSpace(studyCode) || studyCode.Trim().Length > 100)
            throw new ArgumentException("Study code is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(protocolVersion) || protocolVersion.Trim().Length > 100)
            throw new ArgumentException("Protocol version is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(cohortCode) || cohortCode.Trim().Length > 100)
            throw new ArgumentException("Cohort code is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(consentDocumentVersion) || consentDocumentVersion.Trim().Length > 100)
            throw new ArgumentException("Consent document version is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(consentDocumentReference) || consentDocumentReference.Trim().Length > 500)
            throw new ArgumentException("Consent document reference is required and must not exceed 500 characters.");
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
