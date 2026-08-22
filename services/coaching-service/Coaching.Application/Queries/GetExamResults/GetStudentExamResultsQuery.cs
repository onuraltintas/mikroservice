using Coaching.Domain.Enums;

using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetExamResults;

public record GetStudentExamResultsQuery(
    Guid StudentId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<ExamResultDto>>;

public sealed class GetStudentExamResultsQueryValidator : PagedQueryValidator<GetStudentExamResultsQuery>
{
    public GetStudentExamResultsQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record ExamResultDto(
    Guid ExamId,
    string ExamTitle,
    DateTime ExamDate,
    string ExamType,
    decimal Score,
    decimal MaxScore,
    int? CorrectAnswers,
    int? WrongAnswers,
    int? EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
    int? Ranking = null
);
