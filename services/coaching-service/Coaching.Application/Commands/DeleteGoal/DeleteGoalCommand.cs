using MediatR;

namespace Coaching.Application.Commands.DeleteGoal;

public record DeleteGoalCommand(Guid GoalId) : IRequest;
