using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminExam;

public sealed record GetCoachingAdminExamQuery(Guid ExamId)
    : IRequest<CoachingAdminExamDetailDto?>;

public sealed record CoachingAdminExamDetailDto(
    Guid Id,
    Guid CreatedByTeacherId,
    Guid? InstitutionId,
    string Title,
    ExamType ExamType,
    DateTime ExamDate,
    decimal MaxScore,
    string? Description,
    IReadOnlyList<CoachingAdminExamResultDto> Results);

public sealed record CoachingAdminExamResultDto(
    Guid StudentId,
    decimal Score,
    int? CorrectAnswers,
    int? WrongAnswers,
    int? EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
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

        return new CoachingAdminExamDetailDto(
            exam.Id,
            exam.CreatedByTeacherId,
            exam.InstitutionId,
            exam.Title,
            exam.ExamType,
            exam.ExamDate,
            exam.MaxScore,
            exam.Description,
            exam.Results
                .OrderBy(result => result.StudentId)
                .Select(result => new CoachingAdminExamResultDto(
                    result.StudentId,
                    result.Score,
                    result.CorrectAnswers,
                    result.WrongAnswers,
                    result.EmptyAnswers,
                    result.GetSubjectScores(),
                    result.TeacherNotes))
                .ToArray());
    }
}
