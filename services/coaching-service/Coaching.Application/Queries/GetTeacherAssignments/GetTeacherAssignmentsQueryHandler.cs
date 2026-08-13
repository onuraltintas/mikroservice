using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public class GetTeacherAssignmentsQueryHandler : IRequestHandler<GetTeacherAssignmentsQuery, TeacherAssignmentListResponse>
{
    private readonly IAssignmentRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetTeacherAssignmentsQueryHandler(
        IAssignmentRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<TeacherAssignmentListResponse> Handle(
        GetTeacherAssignmentsQuery query, 
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
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
