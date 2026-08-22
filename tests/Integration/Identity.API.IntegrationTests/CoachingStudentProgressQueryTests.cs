using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetStudentProgress;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentProgressQueryTests
{
    [Fact]
    public async Task StudentProgressQuery_ShouldReturnAuthorizedAggregate()
    {
        var studentId = Guid.NewGuid();
        var expected = new StudentProgressSummaryDto(
            studentId, 12, 10, 8, 82.5m, 4, 76.25m, 3, 1, 67, 5, 2, 4, 80m);
        var handler = new GetStudentProgressQueryHandler(
            new StubProgressRepository(expected),
            CreatePolicy(studentId, "Student"),
            new StubIdentityAuthorizationClient([studentId]));

        var result = await handler.Handle(new GetStudentProgressQuery(studentId), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task StudentProgressQuery_ShouldRejectUnauthorizedViewer()
    {
        var studentId = Guid.NewGuid();
        var handler = new GetStudentProgressQueryHandler(
            new StubProgressRepository(null),
            CreatePolicy(Guid.NewGuid(), "Parent"),
            new StubIdentityAuthorizationClient([]));

        var action = () => handler.Handle(new GetStudentProgressQuery(studentId), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubProgressRepository(StudentProgressSummaryDto? summary)
        : ICoachingStudentProgressRepository
    {
        public Task<StudentProgressSummaryDto> GetStudentSummaryAsync(
            Guid studentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary ?? throw new InvalidOperationException("Not expected"));
    }

    private sealed class StubIdentityAuthorizationClient(
        IReadOnlyCollection<Guid> allowedStudentIds) : ICoachingIdentityAuthorizationClient
    {
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
            CancellationToken cancellationToken) =>
            Task.FromResult(allowedStudentIds);
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
