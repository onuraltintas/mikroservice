using MediatR;

namespace Coaching.Application.Commands.DeleteAssignment;

public record DeleteAssignmentCommand(Guid AssignmentId) : IRequest;
