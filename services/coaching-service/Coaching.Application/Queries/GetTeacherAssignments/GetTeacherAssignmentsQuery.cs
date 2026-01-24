using MediatR;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public record GetTeacherAssignmentsQuery(Guid TeacherId) : IRequest<TeacherAssignmentListResponse>;

public record TeacherAssignmentListResponse(List<TeacherAssignmentDto> Assignments);

public record TeacherAssignmentDto(
    Guid Id,
    string Title,
    string Type,
    DateTime DueDate,
    string Status,
    int TotalStudents,
    int SubmittedCount,
    DateTime CreatedAt
);
