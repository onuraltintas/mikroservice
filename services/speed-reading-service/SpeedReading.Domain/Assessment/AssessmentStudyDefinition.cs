using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assessment;

public sealed class AssessmentStudyDefinition : AggregateRoot
{
    private AssessmentStudyDefinition()
    {
    }

    public string StudyCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ProtocolVersion { get; private set; } = string.Empty;
    public string CohortCode { get; private set; } = string.Empty;
    public string ConsentDocumentVersion { get; private set; } = string.Empty;
    public string ConsentDocumentReference { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static AssessmentStudyDefinition Create(
        Guid id, string studyCode, string name, string protocolVersion, string cohortCode,
        string consentDocumentVersion, string consentDocumentReference, Guid actorId, DateTime createdAt)
    {
        Validate(id, studyCode, name, protocolVersion, cohortCode, consentDocumentVersion, consentDocumentReference, actorId);
        return new AssessmentStudyDefinition
        {
            Id = id,
            StudyCode = studyCode.Trim(),
            Name = name.Trim(),
            ProtocolVersion = protocolVersion.Trim(),
            CohortCode = cohortCode.Trim(),
            ConsentDocumentVersion = consentDocumentVersion.Trim(),
            ConsentDocumentReference = consentDocumentReference.Trim(),
            IsActive = true,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId.ToString()
        };
    }

    public void Update(
        string name, string protocolVersion, string cohortCode,
        string consentDocumentVersion, string consentDocumentReference, Guid actorId, DateTime updatedAt)
    {
        Validate(Id, StudyCode, name, protocolVersion, cohortCode, consentDocumentVersion, consentDocumentReference, actorId);
        Name = name.Trim();
        ProtocolVersion = protocolVersion.Trim();
        CohortCode = cohortCode.Trim();
        ConsentDocumentVersion = consentDocumentVersion.Trim();
        ConsentDocumentReference = consentDocumentReference.Trim();
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Retire(Guid actorId, DateTime retiredAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("A valid actor is required.", nameof(actorId));
        IsActive = false;
        UpdatedAt = EnsureUtc(retiredAt);
        UpdatedBy = actorId.ToString();
    }

    private static void Validate(
        Guid id, string studyCode, string name, string protocolVersion, string cohortCode,
        string consentDocumentVersion, string consentDocumentReference, Guid actorId)
    {
        if (id == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Study identifiers are required.");
        if (string.IsNullOrWhiteSpace(studyCode) || studyCode.Trim().Length > 100) throw new ArgumentException("Study code is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Study name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(protocolVersion) || protocolVersion.Trim().Length > 100) throw new ArgumentException("Protocol version is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(cohortCode) || cohortCode.Trim().Length > 100) throw new ArgumentException("Cohort code is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(consentDocumentVersion) || consentDocumentVersion.Trim().Length > 100) throw new ArgumentException("Consent document version is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(consentDocumentReference) || consentDocumentReference.Trim().Length > 500) throw new ArgumentException("Consent document reference is required and must not exceed 500 characters.");
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
