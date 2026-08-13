using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

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

    public SubmitAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
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

        // Save
        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Get submitted student
        var studentAssignment = assignment.AssignedStudents
            .First(s => s.StudentId == command.StudentId);

        // TODO: Publish AssignmentSubmittedEvent to Notification Service

        return new SubmitAssignmentResponse(
            AssignmentId: assignment.Id,
            StudentId: command.StudentId,
            SubmittedAt: studentAssignment.SubmittedAt!.Value,
            Status: studentAssignment.Status.ToString()
        );
    }
}
