using MediatR;

namespace Coaching.Application.Commands.CancelAssignment;

public record CancelAssignmentCommand(Guid AssignmentId) : IRequest;
