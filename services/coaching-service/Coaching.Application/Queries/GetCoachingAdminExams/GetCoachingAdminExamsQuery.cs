using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminExams;

public sealed record GetCoachingAdminExamsQuery(
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    string? ExamType = null,
    string? Search = null) : IRequest<PagedResponse<CoachingAdminExamListDto>>;

public sealed class GetCoachingAdminExamsQueryValidator
    : PagedQueryValidator<GetCoachingAdminExamsQuery>
{
    public GetCoachingAdminExamsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.ExamType)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Enum.TryParse<Domain.Enums.ExamType>(value, true, out _))
            .WithMessage("Exam type is invalid.");
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(query => query.Search is not null);
    }
}
