using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;

using MediatR;

namespace Coaching.Application.Commands.CancelAssignment;

public class CancelAssignmentCommandHandler : IRequestHandler<CancelAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _teacherRecords;
    private readonly ICoachingEventPublisher _eventPublisher;

    public CancelAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient teacherRecords,
        ICoachingEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
        _teacherRecords = teacherRecords;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(CancelAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        await _teacherRecords.RequireCurrentTeacherAsync(_accessPolicy, assignment.TeacherId, assignment.InstitutionId,
            command.IsInstitutionAdminOperation, cancellationToken);

        // Domain logic: Cancel (Soft delete logic). Submitted work stays; every assigned student is told.
        if (assignment.Status != AssignmentStatus.Cancelled)
            await _eventPublisher.PublishAsync(new AssignmentCancelledEvent(assignment.Id, assignment.TeacherId, assignment.InstitutionId,
                assignment.Title, assignment.AssignedStudents.Select(student => student.StudentId).ToArray()), cancellationToken);
        assignment.Cancel();

        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
