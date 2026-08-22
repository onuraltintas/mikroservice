using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using FluentAssertions;
using EduPlatform.Shared.Security.Interfaces;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminScopeTests
{
    [Fact]
    public async Task InstitutionAdministrator_ResolvesTenantScope()
    {
        var institutionId = Guid.NewGuid();
        var client = new StubIdentityAuthorizationClient(
            new CoachingAdminAccessScope(IsGlobal: false, InstitutionId: institutionId));
        var authorization = new CoachingAdminScopeAuthorization(
            new StubCurrentUserService(Guid.NewGuid(), "InstitutionAdmin"),
            client);

        var scope = await authorization.RequireReadScopeAsync(CancellationToken.None);

        scope.IsGlobal.Should().BeFalse();
        scope.InstitutionId.Should().Be(institutionId);
        client.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SystemAdministrator_ResolvesGlobalScopeWithoutIdentityRoundTrip()
    {
        var client = new StubIdentityAuthorizationClient(null);
        var authorization = new CoachingAdminScopeAuthorization(
            new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
            client);

        var scope = await authorization.RequireReadScopeAsync(CancellationToken.None);

        scope.IsGlobal.Should().BeTrue();
        scope.InstitutionId.Should().BeNull();
        client.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Teacher_IsRejectedFromAdministrativeScope()
    {
        var authorization = new CoachingAdminScopeAuthorization(
            new StubCurrentUserService(Guid.NewGuid(), "Teacher"),
            new StubIdentityAuthorizationClient(null));

        var action = () => authorization.RequireReadScopeAsync(CancellationToken.None);

        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    private sealed class StubIdentityAuthorizationClient(CoachingAdminAccessScope? scope)
        : ICoachingIdentityAuthorizationClient
    {
        public List<Guid> Requests { get; } = [];

        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(
            Guid viewerUserId,
            CancellationToken cancellationToken) 
        {
            Requests.Add(viewerUserId);
            return Task.FromResult(scope);
        }

        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken) => Task.FromResult<Guid?>(requestedInstitutionId);

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(studentIds);
    }

    private sealed class StubCurrentUserService(Guid userId, params string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
