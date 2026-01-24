using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Commands.DeleteSession;

public class SessionDeleteHandlers : 
    IRequestHandler<CancelSessionCommand>,
    IRequestHandler<DeleteSessionCommand>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SessionDeleteHandlers(ICoachingSessionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session == null) throw new InvalidOperationException("Session not found");

        session.Cancel();

        await _repository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session == null) throw new InvalidOperationException("Session not found");

        await _repository.DeleteAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
