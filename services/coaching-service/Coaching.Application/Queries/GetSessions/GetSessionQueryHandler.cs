using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetSessions;

public sealed class GetSessionQueryHandler(
    ICoachingSessionRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    : IRequestHandler<GetSessionQuery, SessionDto>
{
    public async Task<SessionDto> Handle(GetSessionQuery query, CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdAsync(query.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {query.SessionId} not found");

        accessPolicy.RequireTeacher(session.TeacherId);
        var studentIds = session.Attendances.Select(attendance => attendance.StudentId).Distinct().ToArray();
        var visibleStudentIds = studentIds.Length == 0
            ? Array.Empty<Guid>()
            : await identityAuthorizationClient.AuthorizeStudentReadAsync(
                accessPolicy.CurrentUserId ?? session.TeacherId,
                studentIds,
                cancellationToken);
        var visible = visibleStudentIds.ToHashSet();
        var visibleAttendance = session.Attendances
            .Where(attendance => visible.Contains(attendance.StudentId))
            .ToArray();

        return new SessionDto(
            Id: session.Id,
            StudentId: visibleAttendance.Select(attendance => attendance.StudentId).FirstOrDefault(),
            StartTime: session.ScheduledDate,
            EndTime: session.ScheduledDate.AddMinutes(session.DurationMinutes),
            DurationMinutes: session.DurationMinutes,
            Subject: session.Title,
            Status: session.Status.ToString(),
            Type: session.SessionType.ToString(),
            StudentIds: visibleAttendance.Select(attendance => attendance.StudentId).ToArray(),
            MeetingLink: session.MeetingLink,
            StudentNote: null,
            StudentReflections: visibleAttendance
                .Where(attendance => !string.IsNullOrWhiteSpace(attendance.StudentNote))
                .OrderBy(attendance => attendance.StudentId)
                .Select(attendance => new SessionStudentReflectionDto(
                    attendance.StudentId,
                    attendance.StudentNote!,
                    attendance.AttendanceStatus.ToString()))
                .ToArray());
    }
}
