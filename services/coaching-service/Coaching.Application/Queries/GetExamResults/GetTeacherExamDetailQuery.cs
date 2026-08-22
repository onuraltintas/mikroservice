using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public sealed record GetTeacherExamDetailQuery(
    Guid ExamId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize)
    : IRequest<TeacherExamDetailDto?>;

public sealed class GetTeacherExamDetailQueryValidator : PagedQueryValidator<GetTeacherExamDetailQuery>
{
    public GetTeacherExamDetailQueryValidator()
    {
        RuleFor(query => query.ExamId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public sealed record TeacherExamDetailDto(
    Guid Id,
    string Title,
    string ExamType,
    string? Subject,
    DateTime ExamDate,
    int? DurationMinutes,
    decimal MaxScore,
    int? TargetGradeLevel,
    string? Description,
    int ResultCount,
    int ResultPageNumber,
    int ResultPageSize,
    int ResultTotalPages,
    IReadOnlyList<TeacherExamResultDto> Results);

public sealed record TeacherExamResultDto(
    Guid Id,
    Guid StudentId,
    decimal Score,
    int? CorrectAnswers,
    int? WrongAnswers,
    int? EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
    int? Ranking,
    string? TeacherNotes);

public sealed class GetTeacherExamDetailQueryHandler
    : IRequestHandler<GetTeacherExamDetailQuery, TeacherExamDetailDto?>
{
    private readonly IExamRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetTeacherExamDetailQueryHandler(
        IExamRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<TeacherExamDetailDto?> Handle(
        GetTeacherExamDetailQuery query,
        CancellationToken cancellationToken)
    {
        var exam = await _repository.GetMetadataByIdAsync(query.ExamId, cancellationToken);
        if (exam is null)
            return null;

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);
        var resultPage = await _repository.GetResultsByExamIdAsync(
            query.ExamId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return new TeacherExamDetailDto(
            exam.Id,
            exam.Title,
            exam.ExamType.ToString(),
            exam.Subject,
            exam.ExamDate,
            exam.DurationMinutes,
            exam.MaxScore,
            exam.TargetGradeLevel,
            exam.Description,
            resultPage.TotalCount,
            query.PageNumber,
            query.PageSize,
            Math.Max(1, (int)Math.Ceiling(resultPage.TotalCount / (double)query.PageSize)),
            resultPage.Items
                .OrderBy(result => result.StudentId)
                .Select(result => new TeacherExamResultDto(
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
