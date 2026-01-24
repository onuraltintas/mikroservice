using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public record GetStudentGoalsQuery(Guid StudentId) : IRequest<List<GoalDto>>;

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
