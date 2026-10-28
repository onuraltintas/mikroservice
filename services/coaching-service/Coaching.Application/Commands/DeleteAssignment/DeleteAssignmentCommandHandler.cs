using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Application.Attachments;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.Extensions.Logging;

using MediatR;

namespace Coaching.Application.Commands.DeleteAssignment;

public class DeleteAssignmentCommandHandler : IRequestHandler<DeleteAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _teacherRecords;
    private readonly IAssignmentAttachmentStorage _attachmentStorage;
    private readonly ICoachingEventPublisher _eventPublisher;
    private readonly ILogger<DeleteAssignmentCommandHandler> _logger;

    public DeleteAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        IAssignmentAttachmentStorage attachmentStorage,
        ICoachingEventPublisher eventPublisher,
        ILogger<DeleteAssignmentCommandHandler> logger,
        ICoachingIdentityAuthorizationClient teacherRecords)
    {
        _teacherRecords = teacherRecords;
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

        await _teacherRecords.RequireCurrentTeacherAsync(_accessPolicy, assignment.TeacherId, assignment.InstitutionId,
            command.IsInstitutionAdminOperation, cancellationToken);

        // Teacher files are removed with the assignment as well; their rows cascade and nothing else
        // points to them.
        var attachments = assignment.AssignedStudents
            .SelectMany(student => student.SubmissionAttachments)
            .Select(attachment => (attachment.Id, attachment.StorageKey))
            .Concat(assignment.InstructionAttachments.Select(attachment => (attachment.Id, attachment.StorageKey)))
            .ToArray();
        foreach (var attachment in attachments)
        {
            await _eventPublisher.PublishAsync(
                new AssignmentAttachmentDeletionRequested(
                    assignment.Id, attachment.Id, attachment.StorageKey),
                cancellationToken);
        }

        // Submitted or graded work is part of the student's record: such an assignment is cancelled, not deleted.
        if (assignment.AssignedStudents.Any(student => student.SubmittedAt.HasValue
                || student.Status is StudentAssignmentStatus.Submitted or StudentAssignmentStatus.Graded))
            throw new BusinessRuleException("Assignment.HasSubmissions",
                "Teslim alınmış ödev silinemez. Ödevi iptal edin; öğrencilerin teslimleri korunur.");
        if (assignment.Status == AssignmentStatus.Active && assignment.AssignedStudents.Count > 0)
            await _eventPublisher.PublishAsync(new AssignmentCancelledEvent(assignment.Id, assignment.TeacherId, assignment.InstitutionId,
                assignment.Title, assignment.AssignedStudents.Select(student => student.StudentId).ToArray()), cancellationToken);

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
