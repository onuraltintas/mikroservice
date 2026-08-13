using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public class GetSessionsQueryHandler : 
    IRequestHandler<GetTeacherSessionsQuery, List<SessionDto>>,
    IRequestHandler<GetUpcomingSessionsQuery, List<SessionDto>>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetSessionsQueryHandler(
        ICoachingSessionRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<List<SessionDto>> Handle(
        GetTeacherSessionsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var sessions = await _repository.GetByTeacherIdAsync(query.TeacherId, cancellationToken);
        return MapToDto(sessions);
    }

    public async Task<List<SessionDto>> Handle(
        GetUpcomingSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var sessions = _accessPolicy.IsSystemAdministrator
            ? await _repository.GetUpcomingSessionsAsync(query.FromDate, cancellationToken)
            : await _repository.GetUpcomingSessionsByTeacherIdAsync(
                _accessPolicy.RequireCurrentTeacher(),
                query.FromDate,
                cancellationToken);
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
