using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public class GetSessionsQueryHandler : 
    IRequestHandler<GetTeacherSessionsQuery, List<SessionDto>>,
    IRequestHandler<GetUpcomingSessionsQuery, List<SessionDto>>
{
    private readonly ICoachingSessionRepository _repository;

    public GetSessionsQueryHandler(ICoachingSessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SessionDto>> Handle(
        GetTeacherSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var sessions = await _repository.GetByTeacherIdAsync(query.TeacherId, cancellationToken);
        return MapToDto(sessions);
    }

    public async Task<List<SessionDto>> Handle(
        GetUpcomingSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var sessions = await _repository.GetUpcomingSessionsAsync(query.FromDate, cancellationToken);
        return MapToDto(sessions);
    }

    private static List<SessionDto> MapToDto(List<Domain.Entities.CoachingSession> sessions)
    {
        return sessions.Select(s => new SessionDto(
            Id: s.Id,
            StudentId: s.Attendances.FirstOrDefault()?.StudentId ?? Guid.Empty,
            StartTime: s.ScheduledDate,
            EndTime: s.ScheduledDate.AddMinutes(s.DurationMinutes),
            DurationMinutes: s.DurationMinutes,
            Subject: s.Title,
            Status: s.Status.ToString(),
            Type: s.SessionType.ToString()
        )).ToList();
    }
}
