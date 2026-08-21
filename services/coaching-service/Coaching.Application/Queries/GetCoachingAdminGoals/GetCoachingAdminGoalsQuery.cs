using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminGoals;

public sealed record GetCoachingAdminGoalsQuery(
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    bool? Completed = null,
    string? Search = null) : IRequest<PagedResponse<CoachingAdminGoalListDto>>;

public sealed class GetCoachingAdminGoalsQueryValidator
    : PagedQueryValidator<GetCoachingAdminGoalsQuery>
{
    public GetCoachingAdminGoalsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(query => query.Search is not null);
    }
}
