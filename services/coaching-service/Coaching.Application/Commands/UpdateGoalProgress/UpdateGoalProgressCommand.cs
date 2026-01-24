using MediatR;

namespace Coaching.Application.Commands.UpdateGoalProgress;

public record UpdateGoalProgressCommand(Guid GoalId, int Progress) : IRequest;
