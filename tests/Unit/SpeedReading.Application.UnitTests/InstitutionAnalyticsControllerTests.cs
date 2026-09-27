using System.Security.Claims;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.UnitTests;

public sealed class InstitutionAnalyticsControllerTests
{
    [Fact]
    public async Task Class_overview_uses_only_the_authorized_institution_scope()
    {
        var institutionId = Guid.NewGuid();
        var scope = new SpeedReadingTeacherStudentScopeResponse([institutionId], [], 7, []);
        var teacherAccess = new StubTeacherAccess { InstitutionScope = scope };
        var reports = new StubTeacherReports();
        var controller = CreateController(teacherAccess, reports);

        var response = await controller.GetClassOverview(institutionId, null, null);

        response.Result.Should().BeOfType<OkObjectResult>();
        reports.LastScope.Should().BeSameAs(scope);
        teacherAccess.LastInstitutionId.Should().Be(institutionId);
        teacherAccess.LastViewerId.Should().Be(Guid.Parse(controller.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
    }

    [Fact]
    public async Task Institution_reports_for_unmanaged_institution_are_forbidden()
    {
        var teacherAccess = new StubTeacherAccess();
        var reports = new StubTeacherReports();
        var controller = CreateController(teacherAccess, reports);

        var response = await controller.GetClassOverview(Guid.NewGuid(), null, null);

        response.Result.Should().BeOfType<ForbidResult>();
        reports.LastScope.Should().BeNull();
    }

    [Fact]
    public async Task Institution_student_detail_requires_active_membership_access()
    {
        var teacherAccess = new StubTeacherAccess { CanReadInstitutionStudent = false };
        var controller = CreateController(teacherAccess, new StubTeacherReports());

        var response = await controller.GetStudentSummary(Guid.NewGuid(), Guid.NewGuid(), null, null);

        response.Result.Should().BeOfType<ForbidResult>();
        teacherAccess.LastStudentId.Should().NotBeEmpty();
    }

    private static InstitutionAnalyticsController CreateController(
        StubTeacherAccess access,
        StubTeacherReports reports)
    {
        var viewerId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, viewerId.ToString()),
            new Claim(ClaimTypes.Role, "InstitutionAdmin")
        ], "Test"));
        var controller = new InstitutionAnalyticsController(reports, new StubAnalytics(), access)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
        return controller;
    }

    private sealed class StubTeacherAccess : ISpeedReadingTeacherAccess
    {
        public SpeedReadingTeacherStudentScopeResponse? InstitutionScope { get; init; }
        public bool CanReadInstitutionStudent { get; init; } = true;
        public Guid LastInstitutionId { get; private set; }
        public Guid LastViewerId { get; private set; }
        public Guid LastStudentId { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            LastViewerId = viewerUserId;
            LastInstitutionId = institutionId;
            return Task.FromResult(InstitutionScope);
        }

        public Task<bool> CanReadInstitutionStudentAsync(
            Guid viewerUserId,
            Guid institutionId,
            Guid studentUserId,
            bool isSystemAdmin = false,
            CancellationToken cancellationToken = default)
        {
            LastViewerId = viewerUserId;
            LastInstitutionId = institutionId;
            LastStudentId = studentUserId;
            return Task.FromResult(CanReadInstitutionStudent);
        }
    }

    private sealed class StubTeacherReports : ILegacySpeedReadingTeacherReports
    {
        public SpeedReadingTeacherStudentScopeResponse? LastScope { get; private set; }

        public Task<TeacherClassOverviewAnalytics> GetClassOverviewAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default)
        {
            LastScope = scope;
            return Task.FromResult(new TeacherClassOverviewAnalytics(
                DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, scope.TotalStudents, 0, false, false,
                false, 0, 0, 0, 0, 0, 0, [], []));
        }

        public Task<TeacherAssignmentAnalytics> GetAssignmentsAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => Task.FromResult<TeacherAssignmentAnalytics>(null!);

        public Task<TeacherContentAnalysisAnalytics> GetContentAnalysisAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => Task.FromResult<TeacherContentAnalysisAnalytics>(null!);

        public Task<TeacherTimeProgressAnalytics> GetTimeProgressAsync(
            SpeedReadingTeacherStudentScopeResponse scope,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default) => Task.FromResult<TeacherTimeProgressAnalytics>(null!);
    }

    private sealed class StubAnalytics : ILegacySpeedReadingAnalytics
    {
        public Task<StudentAnalyticsSummary> GetStudentSummaryAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => Task.FromResult<StudentAnalyticsSummary>(null!);
        public Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => Task.FromResult<StudentReadingSpeedAnalytics>(null!);
        public Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => Task.FromResult<StudentComprehensionAnalytics>(null!);
        public Task<StudentSeriesAnalytics> GetStudentSeriesAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => Task.FromResult<StudentSeriesAnalytics>(null!);
        public Task<StudentActivityAnalytics> GetStudentActivityAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default) => Task.FromResult<StudentActivityAnalytics>(null!);
    }
}
