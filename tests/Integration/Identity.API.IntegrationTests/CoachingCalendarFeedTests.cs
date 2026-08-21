using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetCalendarFeed;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCalendarFeedTests
{
    [Fact]
    public async Task TeacherCalendarFeed_ShouldReturnEscapedIcsWithoutStudentIdentifiers()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var fromDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = fromDate.AddDays(30);
        var repository = new StubCalendarRepository(new CoachingCalendarSession(
            sessionId,
            "Matematik; tekrar\nplanı",
            fromDate.AddDays(1),
            60,
            "Scheduled",
            "https://meet.example.test/session-1",
            new[] { studentId }));
        var handler = new GetCalendarFeedQueryHandler(
            repository,
            CreatePolicy(teacherId, "Teacher"));

        var result = await handler.Handle(
            new GetTeacherCalendarFeedQuery(teacherId, fromDate, toDate),
            CancellationToken.None);

        result.EventCount.Should().Be(1);
        result.ContentType.Should().Be("text/calendar; charset=utf-8");
        result.Content.Should().Contain("BEGIN:VCALENDAR");
        result.Content.Should().Contain("SUMMARY:Matematik\\; tekrar\\nplanı");
        result.Content.Should().Contain($"UID:{sessionId}@coaching");
        result.Content.Should().Contain("LOCATION:https://meet.example.test/session-1");
        result.Content.Should().NotContain(studentId.ToString());
        repository.TeacherRequests.Should().ContainSingle()
            .Which.Should().Be((teacherId, fromDate, toDate));
    }

    [Fact]
    public async Task StudentCalendarFeed_ShouldRejectAnotherStudent()
    {
        var handler = new GetCalendarFeedQueryHandler(
            new StubCalendarRepository(),
            CreatePolicy(Guid.NewGuid(), "Student"));

        var action = () => handler.Handle(
            new GetStudentCalendarFeedQuery(Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public void CalendarFeedValidator_ShouldRejectAnUnboundedDateRange()
    {
        var validator = new GetTeacherCalendarFeedQueryValidator();

        var result = validator.Validate(new GetTeacherCalendarFeedQuery(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(367)));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain("DateRange");
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubCalendarRepository(CoachingCalendarSession? session = null)
        : ICoachingCalendarRepository
    {
        public List<(Guid UserId, DateTime FromDate, DateTime ToDate)> TeacherRequests { get; } = [];

        public Task<IReadOnlyCollection<CoachingCalendarSession>> GetByTeacherIdAsync(
            Guid teacherId,
            DateTime fromDate,
            DateTime toDate,
            int maxEvents,
            CancellationToken cancellationToken = default)
        {
            TeacherRequests.Add((teacherId, fromDate, toDate));
            return Task.FromResult<IReadOnlyCollection<CoachingCalendarSession>>(
                session is null ? [] : new[] { session });
        }

        public Task<IReadOnlyCollection<CoachingCalendarSession>> GetByStudentIdAsync(
            Guid studentId,
            DateTime fromDate,
            DateTime toDate,
            int maxEvents,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CoachingCalendarSession>>(session is null ? [] : new[] { session });
    }

    private sealed class StubCurrentUserService(Guid userId, string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
