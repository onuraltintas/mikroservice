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
    IReadOnlyList<CoachingDataAgreementDto> Agreements)
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
    string? SubjectScoresJson, int? Ranking);

public sealed record CoachingDataGoalDto(
    Guid GoalId, string Title, string? Description, string Category, int CurrentProgress,
    bool IsCompleted, DateTime? TargetDate, decimal? TargetScore);

public sealed record CoachingDataSessionDto(
    Guid SessionId, string Title, DateTime ScheduledDate, int DurationMinutes, string Status,
    string AttendanceStatus, string? StudentNote, string? SharedCoachNote);

public sealed record CoachingDataAgreementDto(
    Guid AcknowledgementId, Guid DocumentId, string DocumentVersion, string Locale,
    string Title, string DocumentReference, string ContentSha256, string PartyRole,
    DateTime AcknowledgedAt, DateTime? WithdrawnAt);

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
        var studentId = accessPolicy.CurrentUserId;
        if (studentId is null || !accessPolicy.IsCurrentStudent(studentId.Value))
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Koçluk veri dışa aktarımı yalnızca ilgili öğrenci hesabı tarafından başlatılabilir.");
        }

        await CoachingStudentReadAuthorization.RequireAsync(
            accessPolicy,
            identityAuthorizationClient,
            [studentId.Value],
            cancellationToken);

        return await repository.ExportStudentDataAsync(
            studentId.Value,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
