using MediatR;

namespace Coaching.Application.Queries.GetStudentAssignments;

public record GetStudentAssignmentsQuery(Guid StudentId) : IRequest<StudentAssignmentListResponse>;

public record StudentAssignmentListResponse(List<StudentAssignmentDto> Assignments);

public record StudentAssignmentDto(
    Guid Id,
    string Title,
    string? Subject,
    DateTime DueDate,
    string Status,
    DateTime? SubmittedAt,
    decimal? Score,
    decimal? MaxScore,
    string? TeacherFeedback,
    bool IsOverdue
);
