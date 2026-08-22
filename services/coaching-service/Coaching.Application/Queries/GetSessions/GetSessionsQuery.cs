using Coaching.Domain.Enums;
using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetSessions;

public record GetTeacherSessionsQuery(
    Guid TeacherId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<SessionDto>>;
public record GetStudentSessionsQuery(
    Guid StudentId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<SessionDto>>;
public record GetUpcomingSessionsQuery(
    DateTime FromDate,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<SessionDto>>;

public record GetSessionQuery(Guid SessionId) : IRequest<SessionDto>;

public sealed record SessionStudentReflectionDto(
    Guid StudentId,
    string Note,
    string AttendanceStatus);

public sealed class GetTeacherSessionsQueryValidator : PagedQueryValidator<GetTeacherSessionsQuery>
{
    public GetTeacherSessionsQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public sealed class GetStudentSessionsQueryValidator : PagedQueryValidator<GetStudentSessionsQuery>
{
    public GetStudentSessionsQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public sealed class GetUpcomingSessionsQueryValidator : PagedQueryValidator<GetUpcomingSessionsQuery>
{
    public GetUpcomingSessionsQueryValidator()
    {
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record SessionDto(
    Guid Id,
    Guid StudentId,
    DateTime StartTime,
    DateTime EndTime,
    int DurationMinutes,
    string? Subject,
    string Status,
    string Type,
    IReadOnlyList<Guid> StudentIds,
    string? MeetingLink,
    string? StudentNote,
    IReadOnlyList<SessionStudentReflectionDto>? StudentReflections = null,
    string? TeacherNotes = null
);
