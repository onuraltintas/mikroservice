using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public class GetTeacherAssignmentsQueryHandler : IRequestHandler<GetTeacherAssignmentsQuery, TeacherAssignmentListResponse>
{
    private readonly IAssignmentRepository _repository;

    public GetTeacherAssignmentsQueryHandler(IAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<TeacherAssignmentListResponse> Handle(
        GetTeacherAssignmentsQuery query, 
        CancellationToken cancellationToken)
    {
        var assignments = await _repository.GetByTeacherIdAsync(query.TeacherId, cancellationToken);

        var dtos = assignments.Select(a => new TeacherAssignmentDto(
            Id: a.Id,
            Title: a.Title,
            Type: a.Type.ToString(),
            DueDate: a.DueDate,
            Status: a.Status.ToString(),
            TotalStudents: a.AssignedStudents.Count,
            SubmittedCount: a.AssignedStudents.Count(s => s.Status == StudentAssignmentStatus.Submitted || 
                                                          s.Status == StudentAssignmentStatus.Graded),
            CreatedAt: a.CreatedAt
        )).ToList();

        return new TeacherAssignmentListResponse(dtos);
    }
}
