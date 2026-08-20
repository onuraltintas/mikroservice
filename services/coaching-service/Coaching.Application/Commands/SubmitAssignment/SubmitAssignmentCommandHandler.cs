using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Contracts.Events.Coaching;

using MediatR;

namespace Coaching.Application.Commands.SubmitAssignment;

/// <summary>
/// Submit Assignment Command Handler
/// </summary>
public class SubmitAssignmentCommandHandler : IRequestHandler<SubmitAssignmentCommand, SubmitAssignmentResponse>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingEventPublisher _eventPublisher;

    public SubmitAssignmentCommandHandler(
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

    public async Task<SubmitAssignmentResponse> Handle(
        SubmitAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        // Get assignment
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        _accessPolicy.RequireStudent(command.StudentId);

        // Submit assignment (domain logic)
        assignment.SubmitAssignment(command.StudentId, command.StudentNote);

        // Get submitted student
        var studentAssignment = assignment.AssignedStudents
            .First(s => s.StudentId == command.StudentId);

        await _eventPublisher.PublishAsync(
            new AssignmentSubmittedEvent(
                assignment.Id,
                command.StudentId,
                studentAssignment.SubmittedAt!.Value),
            cancellationToken);

        // Save the aggregate and its outbox message in one transaction.
        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitAssignmentResponse(
            AssignmentId: assignment.Id,
            StudentId: command.StudentId,
            SubmittedAt: studentAssignment.SubmittedAt!.Value,
            Status: studentAssignment.Status.ToString()
        );
    }
}
