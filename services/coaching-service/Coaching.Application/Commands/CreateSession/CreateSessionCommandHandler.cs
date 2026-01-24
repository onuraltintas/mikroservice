using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using MediatR;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSessionCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateSessionResponse> Handle(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var session = CoachingSession.Create(
            command.TeacherId,
            command.Subject ?? "Coaching Session", // Use subject as Title
            command.StartTime,
            command.Type,
            command.DurationMinutes
        );

        session.AddStudent(command.StudentId);

        if (!string.IsNullOrEmpty(command.Notes))
        {
            session.AddTeacherNotes(command.Notes);
        }

        await _repository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO: Publish SessionScheduledEvent

        return new CreateSessionResponse(session.Id);
    }
}
