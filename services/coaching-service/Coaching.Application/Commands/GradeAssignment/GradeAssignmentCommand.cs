using MediatR;

namespace Coaching.Application.Commands.GradeAssignment;

/// <summary>
/// Grade Assignment Command
/// </summary>
public record GradeAssignmentCommand(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string? TeacherFeedback
) : IRequest<GradeAssignmentResponse>;

public record GradeAssignmentResponse(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string Status,
    DateTime GradedAt
);
