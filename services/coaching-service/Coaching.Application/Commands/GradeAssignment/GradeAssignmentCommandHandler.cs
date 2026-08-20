using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Contracts.Events.Coaching;

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
    private readonly ICoachingEventPublisher _eventPublisher;

    public GradeAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _eventPublisher = eventPublisher;
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

        // Get graded student
        var studentAssignment = assignment.AssignedStudents
            .First(s => s.StudentId == command.StudentId);
        var gradedAt = DateTime.UtcNow;

        await _eventPublisher.PublishAsync(
            new AssignmentGradedEvent(
                assignment.Id,
                command.StudentId,
                command.Score,
                command.TeacherFeedback,
                gradedAt),
            cancellationToken);

        // Save the aggregate and its outbox message in one transaction.
        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GradeAssignmentResponse(
            AssignmentId: assignment.Id,
            StudentId: command.StudentId,
            Score: command.Score,
            Status: studentAssignment.Status.ToString(),
            GradedAt: gradedAt
        );
    }
}
