using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MediatR;

namespace Coaching.Application.Commands.DeleteSession;

public class SessionDeleteHandlers : 
    IRequestHandler<CancelSessionCommand>,
    IRequestHandler<DeleteSessionCommand>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingEventPublisher _eventPublisher;

    public SessionDeleteHandlers(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _eventPublisher = eventPublisher;
    }

    public async Task Handle(CancelSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session == null) throw new InvalidOperationException("Session not found");

        _accessPolicy.RequireTeacher(session.TeacherId);

        session.Cancel();

        await _eventPublisher.PublishAsync(
            new SessionCancelledEvent(
                session.Id,
                session.TeacherId,
                session.InstitutionId,
                session.Attendances.Select(attendance => attendance.StudentId).ToArray(),
                session.ScheduledDate),
            cancellationToken);

        await _repository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session == null) throw new InvalidOperationException("Session not found");

        _accessPolicy.RequireTeacher(session.TeacherId);

        await _repository.DeleteAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
