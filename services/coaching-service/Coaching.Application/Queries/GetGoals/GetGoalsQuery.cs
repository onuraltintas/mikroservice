using Coaching.Domain.Enums;

using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetGoals;

public record GetStudentGoalsQuery(
    Guid StudentId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<GoalDto>>;

public sealed class GetStudentGoalsQueryValidator : PagedQueryValidator<GetStudentGoalsQuery>
{
    public GetStudentGoalsQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record GoalDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    DateTime? TargetDate,
    decimal? TargetScore,
    int Progress,
    bool IsCompleted,
    DateTime? CompletedAt
);
