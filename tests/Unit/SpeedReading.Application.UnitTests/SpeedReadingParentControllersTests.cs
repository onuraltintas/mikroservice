using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Parents;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingParentControllersTests
{
    [Fact]
    public async Task Overview_is_read_only_after_identity_allows_this_parent_and_child()
    {
        var parent = Guid.NewGuid(); var student = Guid.NewGuid();
        var access = new StubAccess { Allowed = true }; var reports = new StubReports();
        var controller = Parents(access, reports, parent);

        var response = await controller.GetOverview(student, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(reports.Overview);
        access.Calls.Should().ContainSingle().Which.Should().Be((parent, student));
        reports.OverviewStudent.Should().Be(student);
    }

    [Fact]
    public async Task Overview_is_forbidden_without_identity_permission_and_reads_nothing()
    {
        var reports = new StubReports();
        var response = await Parents(new StubAccess { Allowed = false }, reports, Guid.NewGuid()).GetOverview(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        reports.OverviewStudent.Should().BeNull();
    }

    [Fact]
    public async Task Overview_rejects_an_empty_child_id()
    {
        var access = new StubAccess { Allowed = true };
        var response = await Parents(access, new StubReports(), Guid.NewGuid()).GetOverview(Guid.Empty, CancellationToken.None);
        response.Should().BeOfType<NotFoundResult>();
        access.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Parent_routes_need_the_parent_role_and_internal_routes_the_service_key()
    {
        typeof(SpeedReadingParentsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/speed-reading/parents/me/children");
        typeof(SpeedReadingParentsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("Parent");
        typeof(InternalSpeedReadingParentsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/internal/speed-reading");
        var summary = typeof(InternalSpeedReadingParentsController).GetMethod(nameof(InternalSpeedReadingParentsController.GetParentWeeklySummary))!;
        summary.GetCustomAttribute<InternalServiceKeyAttribute>().Should().NotBeNull();
        summary.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        summary.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("parent-weekly-summary");
    }

    [Theory]
    [InlineData(null, "2026-10-26")]
    [InlineData("00000000-0000-0000-0000-000000000000", "2026-10-26")]
    [InlineData("8c7f2c55-3f8b-4ad8-9cf3-5f4e8f0f7a10", null)]
    [InlineData("8c7f2c55-3f8b-4ad8-9cf3-5f4e8f0f7a10", "26.10.2026")]
    [InlineData("8c7f2c55-3f8b-4ad8-9cf3-5f4e8f0f7a10", "2026-10-26T00:00:00")]
    public async Task Weekly_summary_validates_its_query(string? student, string? weekStart)
    {
        var reports = new StubReports();
        var response = await new InternalSpeedReadingParentsController(reports)
            .GetParentWeeklySummary(student is null ? null : Guid.Parse(student), weekStart, CancellationToken.None);
        response.Should().BeOfType<BadRequestObjectResult>();
        reports.SummaryStudent.Should().BeNull();
    }

    [Fact]
    public async Task Weekly_summary_is_no_content_without_any_speed_reading_and_ok_otherwise()
    {
        var student = Guid.NewGuid();
        var reports = new StubReports { Summary = null };
        var controller = new InternalSpeedReadingParentsController(reports);
        (await controller.GetParentWeeklySummary(student, "2026-10-26", CancellationToken.None)).Should().BeOfType<NoContentResult>();
        reports.SummaryStudent.Should().Be(student);
        reports.SummaryWeek.Should().Be(new DateOnly(2026, 10, 26));

        reports.Summary = new SpeedReadingParentWeeklySummary(30, 2, 3, 210, 15, 80, 9, 40, 1);
        (await controller.GetParentWeeklySummary(student, "2026-10-26", CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(reports.Summary);
    }

    private static SpeedReadingParentsController Parents(StubAccess access, StubReports reports, Guid parent) =>
        new(access, reports)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, parent.ToString()), new Claim(ClaimTypes.Role, "Parent")], "test"))
                }
            }
        };

    private sealed class StubAccess : ISpeedReadingParentAccess
    {
        public bool Allowed { get; init; }
        public List<(Guid, Guid)> Calls { get; } = [];
        public Task<bool> CanReadAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken = default)
        {
            Calls.Add((parentUserId, studentUserId));
            return Task.FromResult(Allowed);
        }
    }

    private sealed class StubReports : ISpeedReadingParentReports
    {
        public SpeedReadingParentOverview Overview { get; } = new(Guid.NewGuid(), null, null, new(0, 0, 0), [],
            new(null, null, null, []), null, []);
        public SpeedReadingParentWeeklySummary? Summary { get; set; }
        public Guid? OverviewStudent { get; private set; }
        public Guid? SummaryStudent { get; private set; }
        public DateOnly? SummaryWeek { get; private set; }

        public Task<SpeedReadingParentOverview> GetOverviewAsync(Guid studentUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            OverviewStudent = studentUserId;
            return Task.FromResult(Overview);
        }

        public Task<SpeedReadingParentWeeklySummary?> GetWeeklySummaryAsync(Guid studentUserId, DateOnly weekStart, DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            SummaryStudent = studentUserId; SummaryWeek = weekStart;
            return Task.FromResult(Summary);
        }
    }
}

public sealed class SpeedReadingParentCalendarTests
{
    [Theory]
    [InlineData("2026-11-01T20:59:00Z", "2026-10-26")] // Sunday 23:59 in Istanbul
    [InlineData("2026-11-01T21:00:00Z", "2026-11-02")] // Monday 00:00 in Istanbul
    [InlineData("2026-10-28T10:00:00Z", "2026-10-26")]
    public void Weeks_start_on_monday_in_istanbul(string now, string expected)
    {
        var utc = DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        SpeedReadingParentCalendar.Format(SpeedReadingParentCalendar.CurrentWeekStart(utc)).Should().Be(expected);
    }

    [Fact]
    public void A_week_range_is_seven_istanbul_days()
    {
        var (start, end) = SpeedReadingParentCalendar.WeekRangeUtc(new DateOnly(2026, 10, 26));
        start.Should().Be(new DateTime(2026, 10, 25, 21, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 11, 1, 21, 0, 0, DateTimeKind.Utc));
    }
}
