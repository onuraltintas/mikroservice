using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetInstitutionEarlyWarnings;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingEarlyWarningQueryTests
{
    [Fact]
    public async Task EarlyWarningQuery_ShouldScoreDeterministicSignalsAndKeepStudentScope()
    {
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddDays(-30);
        var identity = new StubReportIdentityClient(
            new CoachingStudentReportPage(new[] { studentId }, 3));
        var repository = new StubEarlyWarningRepository(
            new CoachingStudentEarlyWarningMetrics(
                studentId,
                AssignmentCount: 3,
                SubmittedAssignmentCount: 1,
                GradedAssignmentCount: 2,
                AverageAssignmentPercentage: 55m,
                RecordedAttendanceCount: 4,
                AttendedSessionCount: 2,
                GoalCount: 1,
                CompletedGoalCount: 0,
                AverageGoalProgress: 40,
                LastActivityAt: toDate.AddDays(-20)));

        var handler = new GetInstitutionEarlyWarningsQueryHandler(
            repository,
            identity,
            CreatePolicy(Guid.NewGuid(), "SystemAdmin"));

        var result = await handler.Handle(
            new GetInstitutionEarlyWarningsQuery(
                institutionId,
                fromDate,
                toDate,
                GradeLevel: 8,
                PageNumber: 1,
                PageSize: 10),
            CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Should().ContainSingle();
        var warning = result.Items[0];
        warning.StudentId.Should().Be(studentId);
        warning.RiskLevel.Should().Be(EarlyWarningRiskLevel.High);
        warning.RiskScore.Should().Be(100);
        warning.ReasonCodes.Should().BeEquivalentTo(new[]
        {
            EarlyWarningReasonCodes.LowAssignmentSubmission,
            EarlyWarningReasonCodes.LowAssignmentPerformance,
            EarlyWarningReasonCodes.LowAttendance,
            EarlyWarningReasonCodes.LowGoalProgress,
            EarlyWarningReasonCodes.NoRecentActivity
        });
        identity.Requests.Should().ContainSingle().Which.Should().Be(
            (institutionId, 8, 1, 10));
        repository.RequestedStudentIds.Should().BeEquivalentTo(new[] { studentId });
    }

    [Fact]
    public async Task EarlyWarningQuery_ShouldRejectNonAdministrator()
    {
        var handler = new GetInstitutionEarlyWarningsQueryHandler(
            new StubEarlyWarningRepository(),
            new StubReportIdentityClient(new CoachingStudentReportPage([], 0)),
            CreatePolicy(Guid.NewGuid(), "Teacher"));

        var action = () => handler.Handle(
            new GetInstitutionEarlyWarningsQuery(Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public void EarlyWarningQueryValidator_ShouldBoundPagingAndDateRange()
    {
        var validator = new GetInstitutionEarlyWarningsQueryValidator();

        var result = validator.Validate(new GetInstitutionEarlyWarningsQuery(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(367),
            PageNumber: 0,
            PageSize: 101));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "PageNumber", "PageSize", "DateRange" });
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubEarlyWarningRepository(
        CoachingStudentEarlyWarningMetrics? metrics = null)
        : ICoachingEarlyWarningRepository
    {
        public IReadOnlyCollection<Guid> RequestedStudentIds { get; private set; } = [];

        public Task<IReadOnlyCollection<CoachingStudentEarlyWarningMetrics>> GetStudentMetricsAsync(
            Guid institutionId,
            IReadOnlyCollection<Guid> studentIds,
            int? gradeLevel,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            RequestedStudentIds = studentIds;
            return Task.FromResult<IReadOnlyCollection<CoachingStudentEarlyWarningMetrics>>(
                metrics is null ? [] : new[] { metrics });
        }
    }

    private sealed class StubReportIdentityClient(CoachingStudentReportPage page)
        : ICoachingIdentityReportClient
    {
        public List<(Guid InstitutionId, int? GradeLevel, int PageNumber, int PageSize)> Requests { get; } = [];

        public Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
            Guid viewerUserId,
            Guid institutionId,
            int? gradeLevel,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(page.StudentUserIds);

        public Task<CoachingStudentReportPage> GetActiveStudentPageAsync(
            Guid viewerUserId,
            Guid institutionId,
            int? gradeLevel,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Requests.Add((institutionId, gradeLevel, pageNumber, pageSize));
            return Task.FromResult(page);
        }
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
