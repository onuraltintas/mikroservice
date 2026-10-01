using System.Security.Claims;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.UnitTests;

public sealed class TeacherAnalyticsControllerTests
{
    [Fact]
    public async Task GetClassOverview_returns_empty_report_for_teacher_without_students()
    {
        var teacherId = Guid.NewGuid();
        var reports = new TestTeacherReports();
        var controller = CreateController(teacherId, "Teacher", new TestTeacherAccess(), reports);

        var result = await controller.GetClassOverview(null, null);

        result.Result.Should().BeOfType<OkObjectResult>();
        reports.ReceivedScope.Should().BeEquivalentTo(new SpeedReadingTeacherStudentScopeResponse(
            [], [], 0, [], teacherId));
    }

    [Fact]
    public async Task GetClassOverview_does_not_turn_missing_scope_into_an_empty_report_for_other_roles()
    {
        var controller = CreateController(Guid.NewGuid(), "Student", new TestTeacherAccess(), new TestTeacherReports());

        var result = await controller.GetClassOverview(null, null);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    private static TeacherAnalyticsController CreateController(
        Guid viewerId,
        string role,
        TestTeacherAccess teacherAccess,
        TestTeacherReports teacherReports)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, viewerId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            "test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new TeacherAnalyticsController(new TestAnalytics(), teacherReports, teacherAccess)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private sealed class TestTeacherAccess : ISpeedReadingTeacherAccess
    {
        public Task<IReadOnlySet<Guid>> GetReadableStudentIdsAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentUserIds,
            Guid? targetTeacherUserId = null,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> CanReadStudentAsync(
            Guid viewerUserId,
            Guid studentUserId,
            Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false,
            bool isInstitutionAdmin = false,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<SpeedReadingTeacherStudentScopeResponse?> GetStudentScopeAsync(
            Guid viewerUserId,
            Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false,
            bool isInstitutionAdmin = false,
            CancellationToken cancellationToken = default) => Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null);

        public Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionStudentScopeAsync(
            Guid viewerUserId,
            Guid institutionId,
            bool isSystemAdmin = false,
            CancellationToken cancellationToken = default) => Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null);

        public Task<bool> CanReadInstitutionStudentAsync(
            Guid viewerUserId,
            Guid institutionId,
            Guid studentUserId,
            bool isSystemAdmin = false,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class TestTeacherReports : ILegacySpeedReadingTeacherReports
    {
        public SpeedReadingTeacherStudentScopeResponse? ReceivedScope { get; private set; }

        public Task<TeacherClassOverviewAnalytics> GetClassOverviewAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default)
        {
            ReceivedScope = scope;
            return Task.FromResult(new TeacherClassOverviewAnalytics(
                DateTime.UnixEpoch, DateTime.UnixEpoch, 0, 0, false, false, false, 0, 0,
                0, 0, 0, 0, [], []));
        }

        public Task<TeacherAssignmentAnalytics> GetAssignmentsAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TeacherContentAnalysisAnalytics> GetContentAnalysisAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TeacherTimeProgressAnalytics> GetTimeProgressAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestAnalytics : ILegacySpeedReadingAnalytics
    {
        public Task<StudentAnalyticsSummary> GetStudentSummaryAsync(
            Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(
            Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(
            Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StudentSeriesAnalytics> GetStudentSeriesAsync(
            Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StudentActivityAnalytics> GetStudentActivityAsync(
            Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
