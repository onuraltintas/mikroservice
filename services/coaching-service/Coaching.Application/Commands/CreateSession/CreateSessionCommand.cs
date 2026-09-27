using Coaching.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

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
) : IRequest<CreateSessionResponse>
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }

    [JsonIgnore]
    public Guid? InstitutionId { get; init; }
}

public record CreateSessionResponse(Guid SessionId);
