using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.GradeAssignment;

/// <summary>
/// Grade Assignment Command
/// </summary>
public record GradeAssignmentCommand(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string? TeacherFeedback
) : IRequest<GradeAssignmentResponse>
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}

public record GradeAssignmentResponse(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string Status,
    DateTime GradedAt
);
