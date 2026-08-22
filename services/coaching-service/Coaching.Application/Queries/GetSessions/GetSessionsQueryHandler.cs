using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public class GetSessionsQueryHandler : 
    IRequestHandler<GetTeacherSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetStudentSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetUpcomingSessionsQuery, PagedResponse<SessionDto>>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public GetSessionsQueryHandler(
        ICoachingSessionRepository repository,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
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

        var studentIds = page.Items
            .SelectMany(session => session.Attendances)
            .Select(attendance => attendance.StudentId)
            .Distinct()
            .ToArray();
        var visibleStudentIds = studentIds.Length == 0
            ? Array.Empty<Guid>()
            : await _identityAuthorizationClient.AuthorizeStudentReadAsync(
                _accessPolicy.CurrentUserId ?? query.TeacherId,
                studentIds,
                cancellationToken);

        return MapToDto(
            page,
            query.PageNumber,
            query.PageSize,
            visibleStudentIds.ToHashSet(),
            includeStudentReflections: true);
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

    public async Task<PagedResponse<SessionDto>> Handle(
        GetStudentSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var allowedStudentIds = await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var page = await _repository.GetByStudentIdAsync(
            query.StudentId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return MapToDto(
            page,
            query.PageNumber,
            query.PageSize,
            allowedStudentIds,
            includeStudentNote: _accessPolicy.IsCurrentStudent(query.StudentId));
    }

    private static PagedResponse<SessionDto> MapToDto(
        PagedRepositoryResult<Domain.Entities.CoachingSession> page,
        int pageNumber,
        int pageSize,
        IReadOnlySet<Guid>? visibleStudentIds = null,
        bool includeStudentNote = false,
        bool includeStudentReflections = false)
    {
        var sessions = page.Items.Select(s =>
        {
            var studentIds = s.Attendances
                .Select(attendance => attendance.StudentId)
                .Where(studentId => visibleStudentIds is null || visibleStudentIds.Contains(studentId))
                .ToArray();
            var studentNote = includeStudentNote && visibleStudentIds?.Count == 1
                ? s.Attendances.FirstOrDefault(attendance => visibleStudentIds.Contains(attendance.StudentId))?.StudentNote
                : null;
            var studentReflections = includeStudentReflections && visibleStudentIds is not null
                ? s.Attendances
                    .Where(attendance => visibleStudentIds.Contains(attendance.StudentId)
                        && !string.IsNullOrWhiteSpace(attendance.StudentNote))
                    .OrderBy(attendance => attendance.StudentId)
                    .Select(attendance => new SessionStudentReflectionDto(
                        attendance.StudentId,
                        attendance.StudentNote!,
                        attendance.AttendanceStatus.ToString()))
                    .ToArray()
                : Array.Empty<SessionStudentReflectionDto>();

            return new SessionDto(
                Id: s.Id,
                StudentId: studentIds.FirstOrDefault(),
                StartTime: s.ScheduledDate,
                EndTime: s.ScheduledDate.AddMinutes(s.DurationMinutes),
                DurationMinutes: s.DurationMinutes,
                Subject: s.Title,
                Status: s.Status.ToString(),
                Type: s.SessionType.ToString(),
                StudentIds: studentIds,
                MeetingLink: s.MeetingLink,
                StudentNote: studentNote,
                StudentReflections: studentReflections,
                TeacherNotes: includeStudentReflections ? s.TeacherNotes : null);
        }).ToList();

        return new PagedResponse<SessionDto>(sessions, pageNumber, pageSize, page.TotalCount);
    }
}
