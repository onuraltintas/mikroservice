using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetAssignment;

/// <summary>
/// Get Assignment Query Handler
/// </summary>
public class GetAssignmentQueryHandler : IRequestHandler<GetAssignmentQuery, AssignmentResponse?>
{
    private readonly IAssignmentRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetAssignmentQueryHandler(
        IAssignmentRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<AssignmentResponse?> Handle(GetAssignmentQuery query, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(query.AssignmentId, cancellationToken);

        if (assignment == null)
            return null;

        _accessPolicy.RequireTeacherOrAssignedStudent(
            assignment.TeacherId,
            assignment.AssignedStudents.Select(student => student.StudentId));

        var currentUserId = _accessPolicy.CurrentUserId;
        var visibleStudents = !_accessPolicy.IsCurrentTeacher(assignment.TeacherId)
            && currentUserId.HasValue
            && _accessPolicy.IsCurrentStudent(currentUserId.Value)
            ? assignment.AssignedStudents.Where(student =>
                student.StudentId == currentUserId.Value)
            : assignment.AssignedStudents;

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
            AssignedStudents: visibleStudents.Select(s => new AssignedStudentDto(
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
