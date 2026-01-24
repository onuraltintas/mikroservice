using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public record GetTeacherSessionsQuery(Guid TeacherId) : IRequest<List<SessionDto>>;
public record GetUpcomingSessionsQuery(DateTime FromDate) : IRequest<List<SessionDto>>;

public record SessionDto(
    Guid Id,
    Guid StudentId,
    DateTime StartTime,
    DateTime EndTime,
    int DurationMinutes,
    string? Subject,
    string Status,
    string Type
);
