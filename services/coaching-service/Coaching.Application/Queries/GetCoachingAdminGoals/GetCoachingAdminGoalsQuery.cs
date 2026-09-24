using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminGoals;

public sealed record GetCoachingAdminGoalsQuery(
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    bool? Completed = null,
    string? Search = null,
    Guid? InstitutionId = null,
    IReadOnlyCollection<Guid>? ScopedStudentIds = null,
    Guid? StudentId = null) : IRequest<PagedResponse<CoachingAdminGoalListDto>>;

public sealed class GetCoachingAdminGoalsQueryValidator
    : PagedQueryValidator<GetCoachingAdminGoalsQuery>
{
    public GetCoachingAdminGoalsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(query => query.Search is not null);
        RuleFor(query => query.StudentId)
            .NotEmpty()
            .When(query => query.StudentId.HasValue);
    }
}
