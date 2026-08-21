using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Commands.UpdateSessionStudentNote;

public sealed class UpdateSessionStudentNoteCommandHandler(
    ICoachingSessionRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy)
    : IRequestHandler<UpdateSessionStudentNoteCommand>
{
    public async Task Handle(
        UpdateSessionStudentNoteCommand command,
        CancellationToken cancellationToken)
    {
        if (!accessPolicy.IsCurrentStudent(command.StudentId))
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Seans yansımasını yalnızca ilgili öğrenci yazabilir.");
        }

        var session = await repository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
            throw new InvalidOperationException($"Session {command.SessionId} not found");

        var attendance = session.Attendances.FirstOrDefault(item => item.StudentId == command.StudentId);
        if (attendance is null)
            throw new InvalidOperationException($"Student {command.StudentId} is not assigned to this session");

        attendance.AddStudentNote(command.Note?.Trim() ?? string.Empty);
        await repository.UpdateAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
