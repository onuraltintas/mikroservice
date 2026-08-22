using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.UpdateSessionStudentNote;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetSessions;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentSessionsQueryTests
{
    [Fact]
    public async Task StudentSessionQuery_ShouldReturnOnlyRequestedStudentIdentity()
    {
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Matematik koçluğu",
            DateTime.UtcNow.AddDays(1),
            SessionType.Group);
        session.AddStudents([studentId, otherStudentId]);

        var identityClient = new StubIdentityAuthorizationClient([studentId]);
        var handler = new GetSessionsQueryHandler(
            new StubSessionRepository(session),
            CreatePolicy(studentId, "Student"),
            identityClient);

        var result = await handler.Handle(
            new GetStudentSessionsQuery(studentId),
            CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].StudentIds.Should().ContainSingle().Which.Should().Be(studentId);
        identityClient.RequestedStudentIds.Should().ContainSingle().Which.Should().Be(studentId);
    }

    [Fact]
    public async Task StudentSessionQuery_ShouldRejectUnauthorizedParent()
    {
        var studentId = Guid.NewGuid();
        var identityClient = new StubIdentityAuthorizationClient([]);
        var handler = new GetSessionsQueryHandler(
            new StubSessionRepository(),
            CreatePolicy(Guid.NewGuid(), "Parent"),
            identityClient);

        var action = () => handler.Handle(
            new GetStudentSessionsQuery(studentId),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public async Task TeacherSessionQuery_ShouldExposeOnlyIdentityAuthorizedStudentReflections()
    {
        var teacherId = Guid.NewGuid();
        var allowedStudentId = Guid.NewGuid();
        var revokedStudentId = Guid.NewGuid();
        var session = CoachingSession.Create(
            teacherId,
            "Haftalık takip",
            DateTime.UtcNow.AddDays(1),
            SessionType.Group);
        session.AddStudents([allowedStudentId, revokedStudentId]);
        session.Attendances.Single(attendance => attendance.StudentId == allowedStudentId)
            .AddStudentNote("Bu hafta deneme analizini tamamladım.");
        session.Attendances.Single(attendance => attendance.StudentId == revokedStudentId)
            .AddStudentNote("Bu kayıt artık öğretmene görünmemeli.");

        var identityClient = new StubIdentityAuthorizationClient([allowedStudentId]);
        var handler = new GetSessionsQueryHandler(
            new StubSessionRepository(session),
            CreatePolicy(teacherId, "Teacher"),
            identityClient);

        var result = await handler.Handle(
            new GetTeacherSessionsQuery(teacherId),
            CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].StudentIds.Should().ContainSingle().Which.Should().Be(allowedStudentId);
        var reflections = result.Items[0].StudentReflections!;
        reflections.Should().ContainSingle();
        reflections[0].StudentId.Should().Be(allowedStudentId);
        reflections[0].Note.Should().Be("Bu hafta deneme analizini tamamladım.");
        identityClient.RequestedStudentIds.Should().Contain(new[] { allowedStudentId, revokedStudentId });
    }

    [Fact]
    public async Task StudentSessionNoteCommand_ShouldUpdateOnlyAssignedStudentNote()
    {
        var studentId = Guid.NewGuid();
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Matematik koçluğu",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);
        session.AddStudent(studentId);
        var repository = new StubSessionRepository(session);
        var handler = new UpdateSessionStudentNoteCommandHandler(
            repository,
            new StubUnitOfWork(),
            CreatePolicy(studentId, "Student"));

        await handler.Handle(
            new UpdateSessionStudentNoteCommand(session.Id, studentId, "  Bugün tekrar yaptım.  "),
            CancellationToken.None);

        session.Attendances.Single().StudentNote.Should().Be("Bugün tekrar yaptım.");
    }

    [Fact]
    public async Task StudentSessionNoteCommand_ShouldRejectSystemAdministrator()
    {
        var studentId = Guid.NewGuid();
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Matematik koçluğu",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);
        session.AddStudent(studentId);
        var handler = new UpdateSessionStudentNoteCommandHandler(
            new StubSessionRepository(session),
            new StubUnitOfWork(),
            CreatePolicy(Guid.NewGuid(), "SystemAdmin"));

        var action = () => handler.Handle(
            new UpdateSessionStudentNoteCommand(session.Id, studentId, "Yetkisiz not"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
        session.Attendances.Single().StudentNote.Should().BeNull();
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubSessionRepository(params CoachingSession[] sessions)
        : ICoachingSessionRepository
    {
        private readonly IReadOnlyList<CoachingSession> _sessions = sessions;

        public Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_sessions.SingleOrDefault(session => session.Id == id));

        public Task<PagedRepositoryResult<CoachingSession>> GetByTeacherIdAsync(
            Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_sessions.Where(session => session.TeacherId == teacherId), pageNumber, pageSize));

        public Task<PagedRepositoryResult<CoachingSession>> GetByStudentIdAsync(
            Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_sessions.Where(session => session.Attendances.Any(attendance => attendance.StudentId == studentId)), pageNumber, pageSize));

        public Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsAsync(
            DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_sessions.Where(session => session.ScheduledDate >= from), pageNumber, pageSize));

        public Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsByTeacherIdAsync(
            Guid teacherId, DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_sessions.Where(session => session.TeacherId == teacherId && session.ScheduledDate >= from), pageNumber, pageSize));

        public Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default) =>
            Task.FromResult(session);

        public Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static PagedRepositoryResult<CoachingSession> Page(
            IEnumerable<CoachingSession> sessions,
            int pageNumber,
            int pageSize)
        {
            var items = sessions.ToList();
            return new PagedRepositoryResult<CoachingSession>(
                items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                items.Count);
        }
    }

    private sealed class StubIdentityAuthorizationClient(
        IReadOnlyCollection<Guid> allowedStudentIds) : ICoachingIdentityAuthorizationClient
    {
        public IReadOnlyCollection<Guid> RequestedStudentIds { get; private set; } = [];

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
            RequestedStudentIds = studentIds;
            return Task.FromResult(allowedStudentIds);
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
