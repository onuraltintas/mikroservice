using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminSession;

public sealed record GetCoachingAdminSessionQuery(Guid SessionId)
    : IRequest<CoachingAdminSessionDetailDto?>;

public sealed record CoachingAdminSessionDetailDto(
    Guid Id,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    SessionType SessionType,
    DateTime ScheduledDate,
    int DurationMinutes,
    SessionStatus Status,
    IReadOnlyList<CoachingAdminAttendanceDto> Attendances,
    string? MeetingLink);

public sealed record CoachingAdminAttendanceDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? TeacherNote);

public sealed class GetCoachingAdminSessionQueryHandler(
    ICoachingSessionRepository repository)
    : IRequestHandler<GetCoachingAdminSessionQuery, CoachingAdminSessionDetailDto?>
{
    public async Task<CoachingAdminSessionDetailDto?> Handle(
        GetCoachingAdminSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return null;

        return new CoachingAdminSessionDetailDto(
            session.Id,
            session.TeacherId,
            session.InstitutionId,
            session.Title,
            session.SessionType,
            session.ScheduledDate,
            session.DurationMinutes,
            session.Status,
            session.Attendances
                .OrderBy(attendance => attendance.StudentId)
                .Select(attendance => new CoachingAdminAttendanceDto(
                    attendance.StudentId,
                    attendance.AttendanceStatus,
                    attendance.TeacherNote))
                .ToArray(),
            session.MeetingLink);
    }
}
