using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Queries.GetAssignment;

/// <summary>
/// Get Assignment Query Handler
/// </summary>
public class GetAssignmentQueryHandler : IRequestHandler<GetAssignmentQuery, AssignmentResponse?>
{
    private readonly IAssignmentRepository _repository;

    public GetAssignmentQueryHandler(IAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<AssignmentResponse?> Handle(GetAssignmentQuery query, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(query.AssignmentId, cancellationToken);

        if (assignment == null)
            return null;

        return new AssignmentResponse(
            Id: assignment.Id,
            TeacherId: assignment.TeacherId,
            InstitutionId: assignment.InstitutionId,
            Title: assignment.Title,
            Description: assignment.Description,
            Subject: assignment.Subject,
            Type: assignment.Type.ToString(),
            TargetGradeLevel: assignment.TargetGradeLevel,
            DueDate: assignment.DueDate,
            EstimatedDurationMinutes: assignment.EstimatedDurationMinutes,
            MaxScore: assignment.MaxScore,
            PassingScore: assignment.PassingScore,
            Status: assignment.Status.ToString(),
            AssignedStudents: assignment.AssignedStudents.Select(s => new AssignedStudentDto(
                StudentId: s.StudentId,
                SubmittedAt: s.SubmittedAt,
                Score: s.Score,
                TeacherFeedback: s.TeacherFeedback,
                Status: s.Status.ToString()
            )).ToList(),
            CreatedAt: assignment.CreatedAt
        );
    }
}
