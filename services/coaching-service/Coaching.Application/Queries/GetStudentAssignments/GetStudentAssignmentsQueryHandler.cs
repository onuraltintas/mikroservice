using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Application.Authorization;
using Coaching.Application.Queries;

using MediatR;

namespace Coaching.Application.Queries.GetStudentAssignments;

public class GetStudentAssignmentsQueryHandler : IRequestHandler<GetStudentAssignmentsQuery, PagedResponse<StudentAssignmentDto>>
{
    private readonly IAssignmentRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public GetStudentAssignmentsQueryHandler(
        IAssignmentRepository repository,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<PagedResponse<StudentAssignmentDto>> Handle(
        GetStudentAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var page = await _repository.GetByStudentIdAsync(
            query.StudentId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var dtos = page.Items.Select(a =>
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

        return new PagedResponse<StudentAssignmentDto>(
            dtos.ToList(),
            query.PageNumber,
            query.PageSize,
            page.TotalCount);
    }
}
