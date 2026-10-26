using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using Coaching.Application.Authorization;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public record GetTeacherExamsQuery(
    Guid TeacherId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<TeacherExamDto>>;

public sealed class GetTeacherExamsQueryValidator : PagedQueryValidator<GetTeacherExamsQuery>
{
    public GetTeacherExamsQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record TeacherExamDto(
    Guid Id,
    string Title,
    string ExamType,
    DateTime ExamDate,
    decimal MaxScore,
    string? Subject,
    int ResultCount,
    string? Description,
    int? DurationMinutes,
    int? TargetGradeLevel);

public sealed class GetTeacherExamsQueryHandler
    : IRequestHandler<GetTeacherExamsQuery, PagedResponse<TeacherExamDto>>
{
    private readonly IExamRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetTeacherExamsQueryHandler(IExamRepository repository, ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<PagedResponse<TeacherExamDto>> Handle(
        GetTeacherExamsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var page = await _repository.GetTeacherSummariesAsync(
            query.TeacherId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var exams = page.Items.Select(row => new TeacherExamDto(
            row.Exam.Id,
            row.Exam.Title,
            row.Exam.ExamType.ToString(),
            row.Exam.ExamDate,
            row.Exam.MaxScore,
            row.Exam.Subject,
            row.ResultCount,
            row.Exam.Description,
            row.Exam.DurationMinutes,
            row.Exam.TargetGradeLevel)).ToList();

        return new PagedResponse<TeacherExamDto>(exams, query.PageNumber, query.PageSize, page.TotalCount);
    }
}
