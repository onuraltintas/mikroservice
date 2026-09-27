using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetTeacherStudentHistory;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTeacherStudentHistoryQueryTests
{
    [Fact]
    public async Task Handle_ShouldReturnAllHistoryForCurrentlyAuthorizedTeacher()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var history = new CoachingAdminStudentHistoryItemDto(
            Guid.NewGuid(), "Exams", "Matematik denemesi", DateTime.UtcNow, "Completed", 82, 100);
        var repository = new CapturingStudentHistoryRepository([history]);
        var handler = new GetTeacherStudentHistoryQueryHandler(
            repository,
            CreatePolicy(teacherId, "Teacher"),
            new StubIdentityAuthorizationClient([studentId]));

        var result = await handler.Handle(
            new GetTeacherStudentHistoryQuery(studentId, CoachingStudentHistoryType.Exams, 2, 10),
            CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Should().Be(history);
        repository.StudentId.Should().Be(studentId);
        repository.Type.Should().Be(CoachingStudentHistoryType.Exams);
        repository.PageNumber.Should().Be(2);
        repository.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldNotQueryHistoryAfterTeacherAccessWasRevoked()
    {
        var repository = new CapturingStudentHistoryRepository([]);
        var handler = new GetTeacherStudentHistoryQueryHandler(
            repository,
            CreatePolicy(Guid.NewGuid(), "Teacher"),
            new StubIdentityAuthorizationClient([]));

        var action = () => handler.Handle(
            new GetTeacherStudentHistoryQuery(Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
        repository.WasQueried.Should().BeFalse();
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class CapturingStudentHistoryRepository(
        IReadOnlyList<CoachingAdminStudentHistoryItemDto> items) : ICoachingStudentHistoryRepository
    {
        public Guid StudentId { get; private set; }
        public CoachingStudentHistoryType Type { get; private set; }
        public int PageNumber { get; private set; }
        public int PageSize { get; private set; }
        public bool WasQueried { get; private set; }

        public Task<PagedRepositoryResult<CoachingAdminStudentHistoryItemDto>> GetStudentHistoryAsync(
            Guid studentId,
            CoachingStudentHistoryType type,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            WasQueried = true;
            StudentId = studentId;
            Type = type;
            PageNumber = pageNumber;
            PageSize = pageSize;
            return Task.FromResult(new PagedRepositoryResult<CoachingAdminStudentHistoryItemDto>(items, items.Count));
        }
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
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) => Task.FromResult(allowedStudentIds);
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
