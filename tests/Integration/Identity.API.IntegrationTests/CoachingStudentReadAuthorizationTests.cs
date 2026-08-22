using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using FluentAssertions;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentReadAuthorizationTests
{
    [Fact]
    public async Task ParentAuthorization_ShouldKeepOnlyIdentityApprovedStudents()
    {
        var allowedStudentId = Guid.NewGuid();
        var deniedStudentId = Guid.NewGuid();
        var client = new StubIdentityAuthorizationClient(new[] { allowedStudentId });
        var policy = CreatePolicy(Guid.NewGuid(), "Parent");

        var result = await CoachingStudentReadAuthorization.RequireAsync(
            policy,
            client,
            new[] { allowedStudentId, deniedStudentId },
            CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { allowedStudentId });
        client.RequestedStudentIds.Should().BeEquivalentTo(new[] { allowedStudentId, deniedStudentId });
    }

    [Fact]
    public async Task UnauthorizedReader_ShouldBeForbidden()
    {
        var policy = CreatePolicy(Guid.NewGuid(), "InstitutionAdmin");
        var client = new StubIdentityAuthorizationClient(Array.Empty<Guid>());

        var action = () => CoachingStudentReadAuthorization.RequireAsync(
            policy,
            client,
            new[] { Guid.NewGuid() },
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public async Task SystemAdministrator_ShouldCallIdentityForReadAuthorization()
    {
        var studentId = Guid.NewGuid();
        var client = new StubIdentityAuthorizationClient(new[] { studentId });
        var policy = CreatePolicy(Guid.NewGuid(), "SystemAdmin");

        var result = await CoachingStudentReadAuthorization.RequireAsync(
            policy,
            client,
            new[] { studentId },
            CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(studentId);
        client.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StudentReadingOwnData_ShouldCallIdentityForReadAuthorization()
    {
        var studentId = Guid.NewGuid();
        var client = new StubIdentityAuthorizationClient(new[] { studentId });
        var policy = CreatePolicy(studentId, "Student");

        var result = await CoachingStudentReadAuthorization.RequireAsync(
            policy,
            client,
            new[] { studentId },
            CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(studentId);
        client.WasCalled.Should().BeTrue();
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubIdentityAuthorizationClient : ICoachingIdentityAuthorizationClient
    {
        private readonly IReadOnlyCollection<Guid> _allowedStudentIds;

        public StubIdentityAuthorizationClient(IReadOnlyCollection<Guid> allowedStudentIds)
        {
            _allowedStudentIds = allowedStudentIds;
        }

        public bool WasCalled { get; private set; }
        public IReadOnlyCollection<Guid> RequestedStudentIds { get; private set; } = Array.Empty<Guid>();

        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            RequestedStudentIds = studentIds;
            return Task.FromResult(_allowedStudentIds);
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public StubCurrentUserService(Guid userId, string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        public Guid? UserId => _userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
