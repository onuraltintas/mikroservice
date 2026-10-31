using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public class GetSessionsQueryHandler : 
    IRequestHandler<GetTeacherSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetStudentSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetUpcomingSessionsQuery, PagedResponse<SessionDto>>,
    IRequestHandler<GetTeacherAgendaQuery, TeacherAgendaDto>
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
            : await _identityAuthorizationClient.AuthorizeStudentWorkReadAsync(
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

    public async Task<TeacherAgendaDto> Handle(GetTeacherAgendaQuery query, CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var from = TimeZoneInfo.ConvertTimeToUtc(query.WeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Coaching.Domain.CoachingTime.Turkey);
        var to = TimeZoneInfo.ConvertTimeToUtc(query.WeekStart.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Coaching.Domain.CoachingTime.Turkey);
        var sessions = await _repository.GetByTeacherIdInRangeAsync(query.TeacherId, from, to, cancellationToken);
        var studentIds = sessions.SelectMany(session => session.Attendances).Select(attendance => attendance.StudentId).Distinct().ToArray();
        var visible = studentIds.Length == 0 ? Array.Empty<Guid>()
            : await _identityAuthorizationClient.AuthorizeStudentWorkReadAsync(_accessPolicy.CurrentUserId ?? query.TeacherId, studentIds, cancellationToken);
        var page = MapToDto(new PagedRepositoryResult<Domain.Entities.CoachingSession>(sessions.Take(200).ToList(), sessions.Count), 1, 200,
            visible.ToHashSet(), includeStudentReflections: true);
        return new TeacherAgendaDto(query.WeekStart, page.Items.ToArray());
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
        var allowedStudentIds = await CoachingStudentReadAuthorization.RequireWorkAsync(
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
            includeStudentNote: _accessPolicy.IsCurrentStudent(query.StudentId),
            sharedNotesAudience: _accessPolicy.IsCurrentStudent(query.StudentId)
                ? CoachingNoteVisibility.StudentVisible
                : _accessPolicy.IsParent
                    ? CoachingNoteVisibility.GuardianVisible
                    : _accessPolicy.IsInstitutionAdministrator
                        ? CoachingNoteVisibility.InstitutionVisible
                        : null,
            attendanceOf: query.StudentId);
    }

    private static PagedResponse<SessionDto> MapToDto(
        PagedRepositoryResult<Domain.Entities.CoachingSession> page,
        int pageNumber,
        int pageSize,
        IReadOnlySet<Guid>? visibleStudentIds = null,
        bool includeStudentNote = false,
        bool includeStudentReflections = false,
        CoachingNoteVisibility? sharedNotesAudience = null,
        Guid? attendanceOf = null)
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
                    .Where(attendance => visibleStudentIds.Contains(attendance.StudentId))
                    .OrderBy(attendance => attendance.StudentId)
                    .Select(attendance => new SessionStudentReflectionDto(
                        attendance.StudentId,
                        attendance.StudentNote,
                        attendance.AttendanceStatus.ToString()))
                    .ToArray()
                // V3 (parent panel): a student's own list carries that student's attendance as its one reflection row;
                // the note to the teacher only for the student.
                : attendanceOf is { } self && s.Attendances.FirstOrDefault(attendance => attendance.StudentId == self) is { } mine
                    ? new[] { new SessionStudentReflectionDto(mine.StudentId, includeStudentNote ? mine.StudentNote : null, mine.AttendanceStatus.ToString()) }
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
                TeacherNotes: includeStudentReflections ? s.TeacherNotes : null,
                TeacherNotesVisibility: includeStudentReflections && s.TeacherNotes is not null
                    ? s.TeacherNotesVisibility.ToString()
                    : null,
                SharedNotes: sharedNotesAudience == s.TeacherNotesVisibility ? s.TeacherNotes : null,
                SharedNotesVisibility: sharedNotesAudience == s.TeacherNotesVisibility
                    ? s.TeacherNotesVisibility.ToString()
                    : null,
                SeriesId: s.SeriesId,
                // V3 (parent panel): on a student's own list, that student's attendance only.
                AttendanceStatus: attendanceOf is { } own
                    ? s.Attendances.FirstOrDefault(attendance => attendance.StudentId == own)?.AttendanceStatus.ToString()
                    : null);
        }).ToList();

        return new PagedResponse<SessionDto>(sessions, pageNumber, pageSize, page.TotalCount);
    }
}
