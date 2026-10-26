using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Commands.DeleteAssignmentAttachment;

/// <summary>A student removes an own submission file while the assignment still accepts work.</summary>
public sealed record DeleteAssignmentAttachmentCommand(Guid AssignmentId, Guid StudentId, Guid AttachmentId) : IRequest;

public sealed class DeleteAssignmentAttachmentCommandHandler(
    IAssignmentRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    IAssignmentAttachmentStorage storage,
    ICoachingEventPublisher events)
    : IRequestHandler<DeleteAssignmentAttachmentCommand>
{
    public async Task Handle(DeleteAssignmentAttachmentCommand command, CancellationToken cancellationToken)
    {
        accessPolicy.RequireStudent(command.StudentId);
        await CoachingStudentReadAuthorization.RequireAsync(
            accessPolicy, identityAuthorizationClient, [command.StudentId], cancellationToken);
        var assignment = await repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assignment not found.");
        assignment.EnsureAcceptingStudentWork();
        var student = assignment.AssignedStudents.FirstOrDefault(item => item.StudentId == command.StudentId)
            ?? throw new InvalidOperationException("Student is not assigned to this assignment.");
        var attachment = student.RemoveSubmissionAttachment(command.AttachmentId);

        // The outbox event deletes the file if the immediate attempt below fails.
        await events.PublishAsync(new AssignmentAttachmentDeletionRequested(assignment.Id, attachment.Id, attachment.StorageKey), cancellationToken);
        await repository.UpdateAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        try { await storage.DeleteAsync(attachment.StorageKey, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { }
    }
}
