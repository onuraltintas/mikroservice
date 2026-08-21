using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Application.Authorization;
using Coaching.Application.Queries;

using MediatR;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public class GetTeacherAssignmentsQueryHandler : IRequestHandler<GetTeacherAssignmentsQuery, PagedResponse<TeacherAssignmentDto>>
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

    public async Task<PagedResponse<TeacherAssignmentDto>> Handle(
        GetTeacherAssignmentsQuery query, 
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var page = await _repository.GetByTeacherIdAsync(
            query.TeacherId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var dtos = page.Items.Select(a => new TeacherAssignmentDto(
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

        return new PagedResponse<TeacherAssignmentDto>(
            dtos.ToList(),
            query.PageNumber,
            query.PageSize,
            page.TotalCount);
    }
}
