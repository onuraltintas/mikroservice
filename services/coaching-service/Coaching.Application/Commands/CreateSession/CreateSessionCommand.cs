using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Commands.CreateSession;

public record CreateSessionCommand(
    Guid TeacherId,
    Guid StudentId,
    DateTime StartTime,
    int DurationMinutes,
    string? Subject,
    string? Notes,
    SessionType Type,
    string? IdempotencyKey = null,
    IReadOnlyCollection<Guid>? StudentIds = null,
    string? MeetingLink = null,
    CoachingNoteVisibility TeacherNotesVisibility = CoachingNoteVisibility.CoachPrivate
) : IRequest<CreateSessionResponse>;

public record CreateSessionResponse(Guid SessionId);
