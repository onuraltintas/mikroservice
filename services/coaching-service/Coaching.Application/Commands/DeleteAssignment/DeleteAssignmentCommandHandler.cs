using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Application.Attachments;
using Microsoft.Extensions.Logging;

using MediatR;

namespace Coaching.Application.Commands.DeleteAssignment;

public class DeleteAssignmentCommandHandler : IRequestHandler<DeleteAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly IAssignmentAttachmentStorage _attachmentStorage;
    private readonly ICoachingEventPublisher _eventPublisher;
    private readonly ILogger<DeleteAssignmentCommandHandler> _logger;

    public DeleteAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        IAssignmentAttachmentStorage attachmentStorage,
        ICoachingEventPublisher eventPublisher,
        ILogger<DeleteAssignmentCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _attachmentStorage = attachmentStorage;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task Handle(DeleteAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        _accessPolicy.RequireTeacher(assignment.TeacherId);

        var attachments = assignment.AssignedStudents
            .SelectMany(student => student.SubmissionAttachments)
            .ToArray();
        foreach (var attachment in attachments)
        {
            await _eventPublisher.PublishAsync(
                new AssignmentAttachmentDeletionRequested(
                    assignment.Id, attachment.Id, attachment.StorageKey),
                cancellationToken);
        }

        // Hard Delete
        await _repository.DeleteAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The transactionally stored outbox event retries if this immediate attempt fails.
        foreach (var attachment in attachments)
        {
            try
            {
                await _attachmentStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Attachment deletion deferred to outbox for assignment {AssignmentId}, attachment {AttachmentId}",
                    assignment.Id, attachment.Id);
            }
        }
    }
}
