using MediatR;

namespace Coaching.Application.Commands.SubmitAssignment;

/// <summary>
/// Submit Assignment Command
/// </summary>
public record SubmitAssignmentCommand(
    Guid AssignmentId,
    Guid StudentId,
    string? StudentNote
) : IRequest<SubmitAssignmentResponse>;

public record SubmitAssignmentResponse(
    Guid AssignmentId,
    Guid StudentId,
    DateTime SubmittedAt,
    string Status
);
