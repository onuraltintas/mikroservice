using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminSessions;

public sealed record GetCoachingAdminSessionsQuery(
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    string? Status = null,
    string? Search = null,
    Guid? InstitutionId = null) : IRequest<PagedResponse<CoachingAdminSessionListDto>>;

public sealed class GetCoachingAdminSessionsQueryValidator
    : PagedQueryValidator<GetCoachingAdminSessionsQuery>
{
    public GetCoachingAdminSessionsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Status)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Enum.TryParse<SessionStatus>(value, true, out _))
            .WithMessage("Status is invalid.");
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(query => query.Search is not null);
    }
}
