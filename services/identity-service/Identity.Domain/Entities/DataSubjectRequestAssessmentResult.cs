using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public sealed class DataSubjectRequestAssessmentResult : Entity
{
    public Guid RequestId { get; private set; }
    public Guid SubjectUserId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public bool CanProceed { get; private set; }
    public bool HasActiveLegalHold { get; private set; }
    public int AssignmentCount { get; private set; }
    public int AttachmentCount { get; private set; }
    public int ExamResultCount { get; private set; }
    public int GoalCount { get; private set; }
    public int SessionCount { get; private set; }
    public int AgreementCount { get; private set; }
    public DateTime AssessedAt { get; private set; }
    public int TotalRecordCount => AssignmentCount + AttachmentCount + ExamResultCount + GoalCount + SessionCount + AgreementCount;
    private DataSubjectRequestAssessmentResult() { }
    public static DataSubjectRequestAssessmentResult Record(PersonalDataErasureAssessmentCompletedV1 message) => new()
    {
        RequestId = message.RequestId, SubjectUserId = message.SubjectUserId, ServiceName = message.ServiceName,
        CanProceed = message.CanProceed, HasActiveLegalHold = message.HasActiveLegalHold,
        AssignmentCount = message.AssignmentCount, AttachmentCount = message.AttachmentCount,
        ExamResultCount = message.ExamResultCount, GoalCount = message.GoalCount,
        SessionCount = message.SessionCount, AgreementCount = message.AgreementCount,
        AssessedAt = message.AssessedAt, CreatedAt = DateTime.UtcNow
    };
}
