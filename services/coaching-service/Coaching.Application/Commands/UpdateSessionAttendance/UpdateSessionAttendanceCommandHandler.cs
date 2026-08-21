using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Application.Authorization;
using MediatR;

namespace Coaching.Application.Commands.UpdateSessionAttendance;

public class UpdateSessionAttendanceCommandHandler : IRequestHandler<UpdateSessionAttendanceCommand>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public UpdateSessionAttendanceCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(
        UpdateSessionAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {command.SessionId} not found");

        _accessPolicy.RequireTeacher(session.TeacherId);

        var attendance = command.StudentId.HasValue
            ? session.Attendances.FirstOrDefault(item => item.StudentId == command.StudentId.Value)
            : session.Attendances.Count == 1
                ? session.Attendances.First()
                : null;

        if (attendance == null)
        {
            throw new InvalidOperationException(
                session.Attendances.Count > 1
                    ? "StudentId is required for a group session."
                    : "No student assigned to this session");
        }

        // Record attendance
        session.RecordAttendance(attendance.StudentId, command.Attended, command.Notes);

        if (command.Attended && session.Attendances.All(item =>
                item.AttendanceStatus != AttendanceStatus.NotRecorded))
        {
            session.Complete();
        }

        await _repository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
