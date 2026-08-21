using System.Globalization;
using System.Text;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCalendarFeed;

public sealed record GetTeacherCalendarFeedQuery(
    Guid TeacherId,
    DateTime? FromDate = null,
    DateTime? ToDate = null)
    : IRequest<CalendarFeedDto>;

public sealed record GetStudentCalendarFeedQuery(
    Guid StudentId,
    DateTime? FromDate = null,
    DateTime? ToDate = null)
    : IRequest<CalendarFeedDto>;

public sealed class GetTeacherCalendarFeedQueryValidator
    : AbstractValidator<GetTeacherCalendarFeedQuery>
{
    public GetTeacherCalendarFeedQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddDateRules(query => query.FromDate, query => query.ToDate);
    }

    private void AddDateRules(
        Func<GetTeacherCalendarFeedQuery, DateTime?> from,
        Func<GetTeacherCalendarFeedQuery, DateTime?> to)
    {
        RuleFor(query => query)
            .Must(query => !from(query).HasValue
                || !to(query).HasValue
                || from(query)!.Value < to(query)!.Value)
            .WithName("DateRange")
            .WithMessage("Takvim tarih aralığı geçersiz.");
        RuleFor(query => query)
            .Must(query => !from(query).HasValue
                || !to(query).HasValue
                || to(query)!.Value - from(query)!.Value <= TimeSpan.FromDays(366))
            .WithName("DateRange")
            .WithMessage("Takvim aralığı en fazla 366 gün olabilir.");
    }
}

public sealed class GetStudentCalendarFeedQueryValidator
    : AbstractValidator<GetStudentCalendarFeedQuery>
{
    public GetStudentCalendarFeedQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        RuleFor(query => query)
            .Must(query => !query.FromDate.HasValue
                || !query.ToDate.HasValue
                || query.FromDate.Value < query.ToDate.Value)
            .WithName("DateRange")
            .WithMessage("Takvim tarih aralığı geçersiz.");
        RuleFor(query => query)
            .Must(query => !query.FromDate.HasValue
                || !query.ToDate.HasValue
                || query.ToDate.Value - query.FromDate.Value <= TimeSpan.FromDays(366))
            .WithName("DateRange")
            .WithMessage("Takvim aralığı en fazla 366 gün olabilir.");
    }
}

public sealed record CalendarFeedDto(
    string Content,
    string ContentType,
    DateTime FromDate,
    DateTime ToDate,
    int EventCount);

public sealed class GetCalendarFeedQueryHandler(
    ICoachingCalendarRepository repository,
    ICoachingAccessPolicy accessPolicy)
    : IRequestHandler<GetTeacherCalendarFeedQuery, CalendarFeedDto>,
        IRequestHandler<GetStudentCalendarFeedQuery, CalendarFeedDto>
{
    private const int MaxEvents = 500;

    public Task<CalendarFeedDto> Handle(
        GetTeacherCalendarFeedQuery request,
        CancellationToken cancellationToken)
    {
        accessPolicy.RequireTeacher(request.TeacherId);
        return BuildFeed(
            repository.GetByTeacherIdAsync,
            request.TeacherId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
    }

    public Task<CalendarFeedDto> Handle(
        GetStudentCalendarFeedQuery request,
        CancellationToken cancellationToken)
    {
        accessPolicy.RequireStudent(request.StudentId);
        return BuildFeed(
            repository.GetByStudentIdAsync,
            request.StudentId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
    }

    private static async Task<CalendarFeedDto> BuildFeed(
        Func<Guid, DateTime, DateTime, int, CancellationToken, Task<IReadOnlyCollection<CoachingCalendarSession>>> load,
        Guid userId,
        DateTime? requestedFromDate,
        DateTime? requestedToDate,
        CancellationToken cancellationToken)
    {
        var fromDate = requestedFromDate ?? DateTime.UtcNow.AddDays(-30);
        var toDate = requestedToDate ?? DateTime.UtcNow.AddDays(180);
        if (fromDate >= toDate || toDate - fromDate > TimeSpan.FromDays(366))
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Validation.DateRange",
                "Takvim aralığı en fazla 366 gün olabilir.");
        }

        var sessions = await load(userId, fromDate, toDate, MaxEvents, cancellationToken);
        return new CalendarFeedDto(
            CalendarFeedFormatter.Format(sessions),
            "text/calendar; charset=utf-8",
            fromDate,
            toDate,
            sessions.Count);
    }
}

public static class CalendarFeedFormatter
{
    public static string Format(IReadOnlyCollection<CoachingCalendarSession> sessions)
    {
        var builder = new StringBuilder()
            .AppendLine("BEGIN:VCALENDAR")
            .AppendLine("VERSION:2.0")
            .AppendLine("PRODID:-//EduPlatform//Coaching//EN")
            .AppendLine("CALSCALE:GREGORIAN")
            .AppendLine("METHOD:PUBLISH");

        foreach (var session in sessions.OrderBy(item => item.StartTime).ThenBy(item => item.Id))
        {
            var endTime = session.StartTime.AddMinutes(session.DurationMinutes);
            builder
                .AppendLine("BEGIN:VEVENT")
                .AppendLine($"UID:{session.Id}@coaching")
                .AppendLine($"DTSTAMP:{FormatUtc(DateTime.UtcNow)}")
                .AppendLine($"DTSTART:{FormatUtc(session.StartTime)}")
                .AppendLine($"DTEND:{FormatUtc(endTime)}")
                .AppendLine($"SUMMARY:{Escape(session.Title)}")
                .AppendLine($"STATUS:{(session.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ? "CANCELLED" : "CONFIRMED")}");

            if (!string.IsNullOrWhiteSpace(session.MeetingLink))
            {
                builder
                    .AppendLine($"LOCATION:{Escape(session.MeetingLink)}")
                    .AppendLine($"URL:{Escape(session.MeetingLink)}");
            }

            builder.AppendLine("END:VEVENT");
        }

        return builder.AppendLine("END:VCALENDAR").ToString();
    }

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);
}
