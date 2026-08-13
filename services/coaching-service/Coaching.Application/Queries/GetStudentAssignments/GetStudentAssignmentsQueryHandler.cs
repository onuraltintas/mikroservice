using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetStudentAssignments;

public class GetStudentAssignmentsQueryHandler : IRequestHandler<GetStudentAssignmentsQuery, StudentAssignmentListResponse>
{
    private readonly IAssignmentRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetStudentAssignmentsQueryHandler(
        IAssignmentRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<StudentAssignmentListResponse> Handle(
        GetStudentAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireStudent(query.StudentId);
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
