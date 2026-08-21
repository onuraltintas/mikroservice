using Coaching.Application.Authorization;
using Coaching.Application.Commands.AddExamResult;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingWriteIdempotencyTests
{
    [Fact]
    public async Task CreateExam_IsIdempotentPerKey()
    {
        var repository = new InMemoryExamRepository();
        var unitOfWork = new CountingUnitOfWork();
        var publisher = new NoopCoachingEventPublisher();
        var handler = new CreateExamCommandHandler(
            repository,
            unitOfWork,
            new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(),
            new InMemoryIdempotencyRepository(),
            publisher);
        var command = new CreateExamCommand(
            Guid.NewGuid(),
            "Mock exam",
            ExamType.Mock,
            DateTime.UtcNow.AddDays(1),
            100,
            Guid.NewGuid(),
            "Synthetic",
            "exam-key-20260820");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        replay.Should().Be(first);
        repository.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
        publisher.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateSession_IsIdempotentPerKey()
    {
        var repository = new InMemorySessionRepository();
        var unitOfWork = new CountingUnitOfWork();
        var publisher = new NoopCoachingEventPublisher();
        var handler = new CreateSessionCommandHandler(
            repository,
            unitOfWork,
            new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(),
            new InMemoryIdempotencyRepository(),
            publisher);
        var command = new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(2),
            60,
            "Mathematics",
            "Bring notes",
            SessionType.OneOnOne,
            "session-key-20260820");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        replay.Should().Be(first);
        repository.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
        publisher.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateGoal_IsIdempotentPerKey()
    {
        var repository = new InMemoryGoalRepository();
        var unitOfWork = new CountingUnitOfWork();
        var publisher = new NoopCoachingEventPublisher();
        var handler = new CreateGoalCommandHandler(
            repository,
            unitOfWork,
            new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(),
            new InMemoryIdempotencyRepository(),
            publisher);
        var command = new CreateGoalCommand(
            Guid.NewGuid(),
            "Exam preparation",
            GoalCategory.ExamPreparation,
            Guid.NewGuid(),
            "Prepare weekly",
            DateTime.UtcNow.AddDays(10),
            80,
            "goal-key-20260820");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        replay.Should().Be(first);
        repository.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
        publisher.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task AddExamResult_IsIdempotentPerKey()
    {
        var exam = Exam.Create(
            Guid.NewGuid(),
            "Mock exam",
            ExamType.Mock,
            DateTime.UtcNow.AddDays(1),
            100);
        var repository = new InMemoryExamRepository();
        repository.Items.Add(exam);
        var unitOfWork = new CountingUnitOfWork();
        var publisher = new NoopCoachingEventPublisher();
        var handler = new AddExamResultCommandHandler(
            repository,
            unitOfWork,
            new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(),
            new InMemoryIdempotencyRepository(),
            publisher);
        var command = new AddExamResultCommand(
            exam.Id,
            Guid.NewGuid(),
            85,
            17,
            2,
            1,
            new Dictionary<string, decimal> { ["Mathematics"] = 85 },
            "Good work",
            "result-key-20260820");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        exam.Results.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
        publisher.Messages.Should().ContainSingle();
    }

    private sealed class InMemoryIdempotencyRepository : IIdempotencyRepository
    {
        private readonly Dictionary<(string Scope, string Key), IdempotencyRecord> _records = [];

        public Task<IdempotencyRecord?> GetAsync(string scope, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_records.GetValueOrDefault((scope, key)));

        public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            _records[(record.Scope, record.Key)] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryExamRepository : IExamRepository
    {
        public List<Exam> Items { get; } = [];

        public Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(item => item.InstitutionId == institutionId).ToList());

        public Task<PagedRepositoryResult<Exam>> GetByStudentIdAsync(
            Guid studentId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.Results.Any(result => result.StudentId == studentId)).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<Exam>(page, filtered.Count));
        }

        public Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default)
        {
            Items.Add(exam);
            return Task.FromResult(exam);
        }

        public Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default)
        {
            Items.Remove(exam);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySessionRepository : ICoachingSessionRepository
    {
        public List<CoachingSession> Items { get; } = [];

        public Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<PagedRepositoryResult<CoachingSession>> GetByTeacherIdAsync(
            Guid teacherId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.TeacherId == teacherId).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<CoachingSession>(page, filtered.Count));
        }

        public Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsAsync(
            DateTime from,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.ScheduledDate >= from).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<CoachingSession>(page, filtered.Count));
        }

        public Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsByTeacherIdAsync(
            Guid teacherId,
            DateTime from,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.TeacherId == teacherId && item.ScheduledDate >= from).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<CoachingSession>(page, filtered.Count));
        }

        public Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default)
        {
            Items.Add(session);
            return Task.FromResult(session);
        }

        public Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default)
        {
            Items.Remove(session);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryGoalRepository : IAcademicGoalRepository
    {
        public List<AcademicGoal> Items { get; } = [];

        public Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<PagedRepositoryResult<AcademicGoal>> GetByStudentIdAsync(
            Guid studentId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.StudentId == studentId).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<AcademicGoal>(page, filtered.Count));
        }

        public Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default)
        {
            Items.Add(goal);
            return Task.FromResult(goal);
        }

        public Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default)
        {
            Items.Remove(goal);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AllowIdentityAuthorizationClient : ICoachingIdentityAuthorizationClient
    {
        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken) =>
            Task.FromResult(requestedInstitutionId);

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(studentIds.ToArray());
    }

    private sealed class AllowTeacherPolicy : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => Guid.NewGuid();
        public bool IsSystemAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => true;
        public bool IsCurrentStudent(Guid studentId) => false;
        public Guid RequireCurrentTeacher() => Guid.NewGuid();
        public void RequireTeacher(Guid teacherId) { }
        public void RequireStudent(Guid studentId) { }
        public void RequireTeacherOrStudent(Guid teacherId, Guid studentId) { }
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) { }
    }
}
