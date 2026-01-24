using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Queries.GetStudentAssignments;

public class GetStudentAssignmentsQueryHandler : IRequestHandler<GetStudentAssignmentsQuery, StudentAssignmentListResponse>
{
    private readonly IAssignmentRepository _repository;

    public GetStudentAssignmentsQueryHandler(IAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<StudentAssignmentListResponse> Handle(
        GetStudentAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var assignments = await _repository.GetByStudentIdAsync(query.StudentId, cancellationToken);

        var dtos = assignments.Select(a =>
        {
            var studentAssignment = a.AssignedStudents.First(s => s.StudentId == query.StudentId);
            
            return new StudentAssignmentDto(
                Id: a.Id,
                Title: a.Title,
                Subject: a.Subject,
                DueDate: a.DueDate,
                Status: studentAssignment.Status.ToString(),
                SubmittedAt: studentAssignment.SubmittedAt,
                Score: studentAssignment.Score,
                MaxScore: a.MaxScore,
                TeacherFeedback: studentAssignment.TeacherFeedback,
                IsOverdue: a.DueDate < DateTime.UtcNow && 
                          studentAssignment.Status == StudentAssignmentStatus.Assigned
            );
        }).ToList();

        return new StudentAssignmentListResponse(dtos);
    }
}
