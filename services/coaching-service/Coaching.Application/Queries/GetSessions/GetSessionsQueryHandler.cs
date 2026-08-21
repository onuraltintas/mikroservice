using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public class GetSessionsQueryHandler : 
    IRequestHandler<GetTeacherSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetUpcomingSessionsQuery, PagedResponse<SessionDto>>
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

    public async Task<PagedResponse<SessionDto>> Handle(
        GetTeacherSessionsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var page = await _repository.GetByTeacherIdAsync(
            query.TeacherId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return MapToDto(page, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResponse<SessionDto>> Handle(
        GetUpcomingSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var page = _accessPolicy.IsSystemAdministrator
            ? await _repository.GetUpcomingSessionsAsync(query.FromDate, query.PageNumber, query.PageSize, cancellationToken)
            : await _repository.GetUpcomingSessionsByTeacherIdAsync(
                _accessPolicy.RequireCurrentTeacher(),
                query.FromDate,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
        return MapToDto(page, query.PageNumber, query.PageSize);
    }

    private static PagedResponse<SessionDto> MapToDto(
        PagedRepositoryResult<Domain.Entities.CoachingSession> page,
        int pageNumber,
        int pageSize)
    {
        var sessions = page.Items.Select(s =>
        {
            var studentIds = s.Attendances.Select(attendance => attendance.StudentId).ToArray();

            return new SessionDto(
                Id: s.Id,
                StudentId: studentIds.FirstOrDefault(),
                StartTime: s.ScheduledDate,
                EndTime: s.ScheduledDate.AddMinutes(s.DurationMinutes),
                DurationMinutes: s.DurationMinutes,
                Subject: s.Title,
                Status: s.Status.ToString(),
                Type: s.SessionType.ToString(),
                StudentIds: studentIds);
        }).ToList();

        return new PagedResponse<SessionDto>(sessions, pageNumber, pageSize, page.TotalCount);
    }
}
