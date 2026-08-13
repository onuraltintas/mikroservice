using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.GradeAssignment;

/// <summary>
/// Grade Assignment Command Handler
/// </summary>
public class GradeAssignmentCommandHandler : IRequestHandler<GradeAssignmentCommand, GradeAssignmentResponse>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GradeAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task<GradeAssignmentResponse> Handle(
        GradeAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        // Get assignment
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        _accessPolicy.RequireTeacher(assignment.TeacherId);

        // Grade assignment (domain logic)
        assignment.GradeAssignment(
            command.StudentId,
            command.Score,
            command.TeacherFeedback);

        // Save
        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Get graded student
        var studentAssignment = assignment.AssignedStudents
            .First(s => s.StudentId == command.StudentId);

        // TODO: Publish AssignmentGradedEvent to Notification Service

        return new GradeAssignmentResponse(
            AssignmentId: assignment.Id,
            StudentId: command.StudentId,
            Score: command.Score,
            Status: studentAssignment.Status.ToString(),
            GradedAt: DateTime.UtcNow
        );
    }
}
