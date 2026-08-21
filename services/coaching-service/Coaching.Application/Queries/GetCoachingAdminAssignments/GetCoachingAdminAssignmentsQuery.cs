using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminAssignments;

public sealed record GetCoachingAdminAssignmentsQuery(
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    string? Status = null,
    string? Source = null,
    string? Search = null) : IRequest<PagedResponse<CoachingAdminAssignmentListDto>>;

public sealed class GetCoachingAdminAssignmentsQueryValidator
    : PagedQueryValidator<GetCoachingAdminAssignmentsQuery>
{
    public GetCoachingAdminAssignmentsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Status)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Enum.TryParse<AssignmentStatus>(value, true, out _))
            .WithMessage("Status is invalid.");
        RuleFor(query => query.Source)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || Enum.TryParse<AssignmentSource>(value, true, out _))
            .WithMessage("Source is invalid.");
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(query => query.Search is not null);
    }
}
