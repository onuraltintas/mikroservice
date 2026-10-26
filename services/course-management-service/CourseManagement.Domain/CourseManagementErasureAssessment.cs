using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CourseManagement.Domain;

public sealed class CourseManagementErasureAssessment
{
    private CourseManagementErasureAssessment() { }

    public Guid RequestId { get; private set; }
    public Guid SubjectUserId { get; private set; }
    public bool CanProceed { get; private set; }
    public bool HasActiveLegalHold { get; private set; }
    public bool LegalHoldStatusVerified { get; private set; }
    public int AssignmentCount { get; private set; }
    public int AttachmentCount { get; private set; }
    public int SessionCount { get; private set; }
    public int AgreementCount { get; private set; }
    public int AffectedRecordCount { get; private set; }
    public string RecordCountsJson { get; private set; } = "{}";
    public DateTimeOffset AssessedAt { get; private set; }

    [NotMapped]
    public IReadOnlyDictionary<string, int> RecordCounts =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(RecordCountsJson) ?? new Dictionary<string, int>();

    public static CourseManagementErasureAssessment Create(Guid requestId, Guid subjectUserId,
        IReadOnlyDictionary<string, int> recordCounts, CourseManagementLegalHoldStatus legalHoldStatus,
        DateTimeOffset assessedAt)
    {
        if (!Enum.IsDefined(legalHoldStatus)) throw new ArgumentOutOfRangeException(nameof(legalHoldStatus));

        return new CourseManagementErasureAssessment
        {
            RequestId = requestId,
            SubjectUserId = subjectUserId,
            CanProceed = legalHoldStatus == CourseManagementLegalHoldStatus.NoActiveHold,
            HasActiveLegalHold = legalHoldStatus == CourseManagementLegalHoldStatus.ActiveHold,
            LegalHoldStatusVerified = legalHoldStatus != CourseManagementLegalHoldStatus.Unknown,
            AssignmentCount = recordCounts.GetValueOrDefault("assignments"),
            AttachmentCount = recordCounts.GetValueOrDefault("attachments"),
            SessionCount = recordCounts.GetValueOrDefault("lessonSessions")
                + recordCounts.GetValueOrDefault("attendanceRecords"),
            AgreementCount = recordCounts.GetValueOrDefault("feeAgreements"),
            AffectedRecordCount = recordCounts.Values.Sum(),
            RecordCountsJson = JsonSerializer.Serialize(recordCounts),
            AssessedAt = assessedAt.ToUniversalTime()
        };
    }
}
