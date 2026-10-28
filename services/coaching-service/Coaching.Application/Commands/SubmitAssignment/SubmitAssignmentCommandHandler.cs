using EduPlatform.Shared.Kernel.Exceptions;
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

    // Every submission changes the shared assignment row (its version guards against a concurrent cancel),
    // so two students submitting in the same moment conflict. The loser retries on fresh data instead of
    // failing with an error close to the deadline.
    private const int Attempts = 3;

    public async Task<SubmitAssignmentResponse> Handle(
        SubmitAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireStudent(command.StudentId);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SubmitAsync(command, cancellationToken);
            }
            catch (ConcurrencyException) when (attempt < Attempts)
            {
                _unitOfWork.DiscardChanges();
            }
        }
    }

    private async Task<SubmitAssignmentResponse> SubmitAsync(SubmitAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        assignment.SubmitAssignment(command.StudentId, command.StudentNote);
        var studentAssignment = assignment.AssignedStudents.First(s => s.StudentId == command.StudentId);

        await _eventPublisher.PublishAsync(
            new AssignmentSubmittedEvent(
                assignment.Id,
                command.StudentId,
                studentAssignment.SubmittedAt!.Value,
                assignment.TeacherId),
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
