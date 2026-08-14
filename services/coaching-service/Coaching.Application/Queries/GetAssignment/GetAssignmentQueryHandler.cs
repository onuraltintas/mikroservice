using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;

using MediatR;

namespace Coaching.Application.Queries.GetAssignment;

/// <summary>
/// Get Assignment Query Handler
/// </summary>
public class GetAssignmentQueryHandler : IRequestHandler<GetAssignmentQuery, AssignmentResponse?>
{
    private readonly IAssignmentRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public GetAssignmentQueryHandler(
        IAssignmentRepository repository,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<AssignmentResponse?> Handle(GetAssignmentQuery query, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(query.AssignmentId, cancellationToken);

        if (assignment == null)
            return null;

        var allowedStudentIds = await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            assignment.AssignedStudents.Select(student => student.StudentId).ToArray(),
            cancellationToken);

        var visibleStudents = assignment.AssignedStudents.Where(student =>
            allowedStudentIds.Contains(student.StudentId));

        if (!visibleStudents.Any())
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Bu assignment verisine erişim yetkiniz yok.");
        }

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
