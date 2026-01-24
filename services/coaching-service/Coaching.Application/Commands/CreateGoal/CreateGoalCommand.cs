using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Commands.CreateGoal;

public record CreateGoalCommand(
    Guid StudentId,
    string Title,
    GoalCategory Category,
    Guid? TeacherId,
    string? Description,
    DateTime? TargetDate,
    decimal? TargetScore
) : IRequest<CreateGoalResponse>;

public record CreateGoalResponse(Guid GoalId);
