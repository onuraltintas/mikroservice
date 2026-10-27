using Coaching.Application.Authorization;
using Coaching.Application.CoachingAgreements;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Queries.ExportCoachingData;

public sealed record ExportCoachingDataQuery : IRequest<CoachingDataExportDto>, IBypassesCoachingAgreementRequirement;

public sealed record CoachingDataExportDto(
    string SchemaVersion,
    Guid StudentId,
    DateTimeOffset ExportedAt,
    IReadOnlyList<CoachingDataAssignmentDto> Assignments,
    IReadOnlyList<CoachingDataExamDto> Exams,
    IReadOnlyList<CoachingDataGoalDto> Goals,
    IReadOnlyList<CoachingDataSessionDto> Sessions,
    IReadOnlyList<CoachingDataAgreementDto> Agreements,
    IReadOnlyList<CoachingDataStudyPlanDto>? StudyPlans = null,
    Coaching.Application.StudyPlanning.StudyAvailabilityView? StudyAvailability = null,
    IReadOnlyList<CoachingDataReminderDto>? Reminders = null,
    IReadOnlyList<CoachingDataFeeDto>? Fees = null,
    Coaching.Application.Profiles.CoachingOwnProfile? Profile = null,
    IReadOnlyList<Coaching.Domain.Profiles.CoachingTeacherStudentAssignment>? ProfileAssignments = null,
    Coaching.Domain.Entities.StudentLearningContext? LearningContext = null,
    Coaching.Application.StudyPlanning.WeeklyDraftView? WeeklyDraft = null,
    IReadOnlyList<Coaching.Domain.Entities.WeeklyReflection>? WeeklyReflections = null,
    IReadOnlyList<Coaching.Domain.Entities.WeeklyDecision>? WeeklyDecisions = null,
    IReadOnlyList<Coaching.Domain.Entities.WeeklyReflectionReview>? WeeklyReflectionReviews = null,
    IReadOnlyList<CoachingDataSubscriptionDto>? Subscriptions = null,
    IReadOnlyList<CoachingDataSeatDto>? SubscriptionSeats = null,
    IReadOnlyList<CoachingDataBankTransferDto>? BankTransfers = null,
    IReadOnlyList<CoachingDataPaymentDto>? Payments = null,
    IReadOnlyList<CoachingDataConversationDto>? Conversations = null)
{
    public static CoachingDataExportDto Empty(Guid studentId, DateTimeOffset exportedAt) =>
        new("1.0", studentId, exportedAt, [], [], [], [], []);
}

public sealed record CoachingDataAssignmentDto(
    Guid AssignmentId, string Title, string? Subject, DateTime DueDate, string Status,
    DateTime? SubmittedAt, decimal? Score, string? StudentNote, string? TeacherFeedback);

public sealed record CoachingDataExamDto(
    Guid ExamId, string Title, string? Subject, DateTime ExamDate, decimal MaxScore,
    decimal Score, int? CorrectAnswers, int? WrongAnswers, int? EmptyAnswers,
    string? SubjectScoresJson, int? Ranking, string Source = "TeacherRecorded",
    IReadOnlyList<Coaching.Domain.Entities.LessonAnswerStatistics>? LessonAnswers = null);

public sealed record CoachingDataStudyPlanDto(Guid Id, Guid PlanId, int RevisionNumber, int Version, string Title,
    string Status, int? AutomaticAvailabilityVersion, Guid? AutomaticSourceRevisionId, int? AutomaticSourceRevisionVersion,
    IReadOnlyList<Coaching.Application.StudyPlanning.ManualStudyTaskView> Tasks,Coaching.Domain.Entities.WeeklyPlanSource? WeeklySource=null,Coaching.Domain.Entities.AutomaticOutcomeEvidence? AutomaticOutcomeEvidence=null);

public sealed record CoachingDataGoalDto(
    Guid GoalId, string Title, string? Description, string Category, int CurrentProgress,
    bool IsCompleted, DateTime? TargetDate, decimal? TargetScore, Guid? TargetSchoolId = null, Guid? TargetUniversityProgramId = null,
    decimal? TargetMaxScore = null, string? TargetExamType = null, string? TargetSubject = null);

public sealed record CoachingDataSessionDto(
    Guid SessionId, string Title, DateTime ScheduledDate, int DurationMinutes, string Status,
    string AttendanceStatus, string? StudentNote, string? SharedCoachNote);

public sealed record CoachingDataAgreementDto(
    Guid AcknowledgementId, Guid DocumentId, string DocumentVersion, string Locale,
    string Title, string DocumentReference, string ContentSha256, string PartyRole,
    DateTime AcknowledgedAt, DateTime? WithdrawnAt);
public sealed record CoachingDataSubscriptionDto(Guid Id, Guid PlanId, string? PlanName, Guid? InstitutionId, string Status,
    DateTime StartDate, DateTime EndDate, string? PaymentReference, Guid? BankTransferRequestId, DateTime CreatedAt);
public sealed record CoachingDataSeatDto(Guid Id, Guid SubscriptionId, bool IsSuspended, string? SuspensionReason, DateTime AssignedAt);
public sealed record CoachingDataBankTransferDto(Guid Id, Guid PlanId, decimal Amount, string Currency, string PaymentReference,
    string? PayerName, string? Note, string Status, Guid? SubscriptionId, DateTime? ReviewedAt, string? ReviewNote, DateTime CreatedAt);
public sealed record CoachingDataPaymentDto(Guid Id, Guid PlanId, string PlanName, Guid? SubscriptionId, Guid? BankTransferRequestId,
    decimal Amount, string Currency, string Status, string Provider, string? Reference, DateTime CreatedAt);
/// <summary>Roadmap O3: the student's own conversations with their teachers (a parent's conversations are the parent's data).</summary>
public sealed record CoachingDataConversationDto(Guid Id, Guid TeacherUserId, DateTime CreatedAt, IReadOnlyList<CoachingDataMessageDto> Messages);
public sealed record CoachingDataMessageDto(Guid Id, Guid SenderUserId, string Body, DateTime SentAt, IReadOnlyList<string> AttachmentNames);
public sealed record CoachingDataReminderDto(Guid Id, Guid SourceId, string Kind, DateTime DueAt, DateTime QueuedAt);
public sealed record CoachingDataFeeInstallmentDto(Guid Id, int Sequence, DateOnly DueOn, decimal Amount);
public sealed record CoachingDataFeeDto(Guid Id, Guid? InstitutionId, Guid? CoachId, Guid CreatedByUserId, string Title,
    string Currency, decimal TotalAmount, decimal PaidAmount, bool IsCancelled, Guid? CancelledByUserId,
    string? CancellationReason, DateTime? CancelledAt, DateTime CreatedAt,
    IReadOnlyList<CoachingDataFeeInstallmentDto> Installments, IReadOnlyList<Coaching.Application.Finance.CoachingFeePaymentView> Payments);

public sealed class ExportCoachingDataQueryHandler(
    ICoachingDataExportRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    TimeProvider timeProvider) : IRequestHandler<ExportCoachingDataQuery, CoachingDataExportDto>
{
    public async Task<CoachingDataExportDto> Handle(
        ExportCoachingDataQuery request,
        CancellationToken cancellationToken)
    {
        // Every coaching user exports only their own data; the id always comes from the token.
        var studentId = accessPolicy.CurrentUserId;
        if (studentId is null)
            throw Forbidden();
        if (accessPolicy.IsCurrentStudent(studentId.Value))
        {
            await CoachingStudentReadAuthorization.RequireAsync(
                accessPolicy,
                identityAuthorizationClient,
                [studentId.Value],
                cancellationToken);
        }
        else if (!accessPolicy.IsCurrentTeacher(studentId.Value))
        {
            throw Forbidden();
        }

        return await repository.ExportStudentDataAsync(
            studentId.Value,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static BusinessRuleException Forbidden() => new(
        "Authorization.Forbidden",
        "Koçluk veri dışa aktarımı yalnızca ilgili öğrenci veya öğretmen hesabı tarafından başlatılabilir.");
}
