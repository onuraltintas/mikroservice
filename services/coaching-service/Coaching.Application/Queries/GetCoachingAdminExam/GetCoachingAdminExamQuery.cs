using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminExam;

public sealed record GetCoachingAdminExamQuery(
    Guid ExamId,
    Guid? InstitutionId = null,
    bool AdministrativeScope = false,
    IReadOnlyCollection<Guid>? ScopedStudentIds = null)
    : IRequest<CoachingAdminExamDetailDto?>;

public sealed record CoachingAdminExamDetailDto(
    Guid Id,
    Guid CreatedByTeacherId,
    Guid? InstitutionId,
    string Title,
    ExamType ExamType,
    string? Subject,
    DateTime ExamDate,
    int? DurationMinutes,
    decimal MaxScore,
    int? TargetGradeLevel,
    string? Description,
    IReadOnlyList<CoachingAdminExamResultDto> Results);

public sealed record CoachingAdminExamResultDto(
    Guid Id,
    Guid StudentId,
    decimal Score,
    int? CorrectAnswers,
    int? WrongAnswers,
    int? EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
    int? Ranking,
    string? TeacherNotes);

public sealed class GetCoachingAdminExamQueryHandler(
    IExamRepository repository)
    : IRequestHandler<GetCoachingAdminExamQuery, CoachingAdminExamDetailDto?>
{
    public async Task<CoachingAdminExamDetailDto?> Handle(
        GetCoachingAdminExamQuery request,
        CancellationToken cancellationToken)
    {
        var exam = await repository.GetByIdAsync(request.ExamId, cancellationToken);
        if (exam is null)
            return null;

        if (request.InstitutionId.HasValue
            && exam.InstitutionId != request.InstitutionId)
        {
            return null;
        }

        return new CoachingAdminExamDetailDto(
            exam.Id,
            exam.CreatedByTeacherId,
            exam.InstitutionId,
            exam.Title,
            exam.ExamType,
            exam.Subject,
            exam.ExamDate,
            exam.DurationMinutes,
            exam.MaxScore,
            exam.TargetGradeLevel,
            exam.Description,
            (request.AdministrativeScope && request.InstitutionId.HasValue
                ? exam.Results.Where(result =>
                    request.ScopedStudentIds?.Contains(result.StudentId) == true)
                : exam.Results)
                .OrderBy(result => result.StudentId)
                .Select(result => new CoachingAdminExamResultDto(
                    result.Id,
                    result.StudentId,
                    result.Score,
                    result.CorrectAnswers,
                    result.WrongAnswers,
                    result.EmptyAnswers,
                    result.GetSubjectScores(),
                    result.Ranking,
                    result.TeacherNotes))
                .ToArray());
    }
}
