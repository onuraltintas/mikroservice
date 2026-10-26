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
        var page = await _repository.GetTeacherSummariesAsync(
            query.TeacherId,
            query.PageNumber,
            query.PageSize,
            cancellationToken,
            query.Status);

        var dtos = page.Items.Select(row => new TeacherAssignmentDto(
            Id: row.Assignment.Id,
            Title: row.Assignment.Title,
            Type: row.Assignment.Type.ToString(),
            DueDate: row.Assignment.DueDate,
            Status: row.Assignment.Status.ToString(),
            TotalStudents: row.TotalStudents,
            SubmittedCount: row.SubmittedCount,
            CreatedAt: row.Assignment.CreatedAt
        )).ToList();

        return new PagedResponse<TeacherAssignmentDto>(
            dtos.ToList(),
            query.PageNumber,
            query.PageSize,
            page.TotalCount);
    }
}
