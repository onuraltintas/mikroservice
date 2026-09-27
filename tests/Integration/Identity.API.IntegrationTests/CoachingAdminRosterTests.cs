using System.Security.Claims;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminRosterTests
{
    [Fact]
    public async Task StudentRoster_ForwardsGradeFilterToIdentityReportScope()
    {
        var viewerId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();
        var reportClient = new CapturingReportClient();
        var controller = CreateController();

        var result = await controller.GetStudentRoster(
            institutionId,
            reportClient,
            new TestCurrentUserService(viewerId),
            gradeLevel: 8,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        reportClient.RequestedGradeLevel.Should().Be(8);
        reportClient.StudentPageCallCount.Should().Be(1);
    }

    [Fact]
    public async Task StudentRoster_RejectsGradeOutsideSupportedRange()
    {
        var viewerId = Guid.NewGuid();
        var reportClient = new CapturingReportClient();
        var controller = CreateController();

        var result = await controller.GetStudentRoster(
            Guid.NewGuid(),
            reportClient,
            new TestCurrentUserService(viewerId),
            gradeLevel: 13,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
        reportClient.RequestedGradeLevel.Should().BeNull();
        reportClient.StudentPageCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StudentRoster_DoesNotQueryWhenInstitutionIsOutsideAuthenticatedScope()
    {
        var viewerId = Guid.NewGuid();
        var reportClient = new CapturingReportClient();
        var controller = new CoachingAdminController(null!, new FixedScopeAuthorization(new CoachingAdminScope(
            IsGlobal: false,
            InstitutionId: Guid.NewGuid(),
            StudentIds: [])), null!);

        var result = await controller.GetStudentRoster(
            Guid.NewGuid(),
            reportClient,
            new TestCurrentUserService(viewerId),
            gradeLevel: 8,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        reportClient.RequestedGradeLevel.Should().BeNull();
        reportClient.StudentPageCallCount.Should().Be(0);
    }

    private static CoachingAdminController CreateController() =>
        new(null!, new FixedScopeAuthorization(new CoachingAdminScope(
            IsGlobal: true,
            InstitutionId: null,
            StudentIds: null)), null!);

    private sealed class FixedScopeAuthorization(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }

    private sealed class CapturingReportClient : ICoachingIdentityReportClient
    {
        public int? RequestedGradeLevel { get; private set; }
        public int StudentPageCallCount { get; private set; }

        public Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
            Guid viewerUserId, Guid institutionId, int? gradeLevel, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<CoachingStudentReportPage> GetActiveStudentPageAsync(
            Guid viewerUserId,
            Guid institutionId,
            int? gradeLevel,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken,
            string? search = null,
            Guid? teacherUserId = null)
        {
            StudentPageCallCount++;
            RequestedGradeLevel = gradeLevel;
            return Task.FromResult(new CoachingStudentReportPage([], 0));
        }

        public Task<CoachingTeacherReportPage> GetActiveTeacherPageAsync(
            Guid viewerUserId, Guid institutionId, int pageNumber, int pageSize,
            string? search, CancellationToken cancellationToken) =>
            Task.FromResult(new CoachingTeacherReportPage([], 0));
    }

    private sealed class TestCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
