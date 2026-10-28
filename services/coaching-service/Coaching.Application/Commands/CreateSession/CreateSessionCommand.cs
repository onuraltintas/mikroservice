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
    CoachingNoteVisibility TeacherNotesVisibility = CoachingNoteVisibility.CoachPrivate,
    SessionRecurrence? Recurrence = null
) : IRequest<CreateSessionResponse>
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }

    [JsonIgnore]
    public Guid? InstitutionId { get; init; }
}

/// <summary>Roadmap O6: repeat every <paramref name="IntervalWeeks"/> week(s) (1 or 2), <paramref name="Count"/> sessions in all (2–26).</summary>
public record SessionRecurrence(int IntervalWeeks, int Count)
{
    public const int MaxCount = 26;
}

/// <summary>The first session; for a series also the series id and the number of sessions created.</summary>
public record CreateSessionResponse(Guid SessionId, Guid? SeriesId = null, int Count = 1);
