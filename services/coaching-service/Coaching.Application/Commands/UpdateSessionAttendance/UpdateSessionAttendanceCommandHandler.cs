using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Commands.UpdateSessionAttendance;

public class UpdateSessionAttendanceCommandHandler : IRequestHandler<UpdateSessionAttendanceCommand>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSessionAttendanceCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        UpdateSessionAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {command.SessionId} not found");

        // Assuming single student session for now (MVP)
        var attendance = session.Attendances.FirstOrDefault();
        if (attendance == null)
            throw new InvalidOperationException("No student assigned to this session");

        // Record attendance
        session.RecordAttendance(attendance.StudentId, command.Attended, command.Notes);

        if (command.Attended)
        {
            session.Complete();
        }

        await _repository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
