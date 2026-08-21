using Coaching.Application.Authorization;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAssignmentIdempotencyTests
{
    [Fact]
    public async Task CreateAssignment_IsIdempotentPerKeyAndRejectsPayloadChanges()
    {
        var assignments = new InMemoryAssignmentRepository();
        var idempotency = new InMemoryIdempotencyRepository();
        var unitOfWork = new CountingUnitOfWork();
        var publisher = new NoopCoachingEventPublisher();
        var handler = new CreateAssignmentCommandHandler(
            assignments,
            unitOfWork,
            new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(),
            idempotency,
            publisher);

        var key = "assignment-key-20260820";
        var command = new CreateAssignmentCommand
        {
            TeacherId = Guid.NewGuid(),
            InstitutionId = Guid.NewGuid(),
            Title = "Algebra practice",
            Description = "Linear equations",
            Subject = "Mathematics",
            AssignmentType = "Individual",
            DueDate = DateTime.UtcNow.AddDays(2),
            EstimatedDurationMinutes = 45,
            MaxScore = 100,
            PassingScore = 60,
            StudentIds = [Guid.NewGuid(), Guid.NewGuid()],
            IdempotencyKey = key
        };

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        replay.AssignmentId.Should().Be(first.AssignmentId);
        assignments.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
        publisher.Messages.Should().ContainSingle();

        var changed = command with { Title = "Different payload" };
        var conflict = () => handler.Handle(changed, CancellationToken.None);

        var exception = await conflict.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("Idempotency.Conflict");
    }

    private sealed class InMemoryAssignmentRepository : IAssignmentRepository
    {
        public List<Assignment> Items { get; } = [];

        public Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<PagedRepositoryResult<Assignment>> GetByTeacherIdAsync(
            Guid teacherId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.TeacherId == teacherId).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<Assignment>(page, filtered.Count));
        }

        public Task<PagedRepositoryResult<Assignment>> GetByStudentIdAsync(
            Guid studentId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(item => item.AssignedStudents.Any(student => student.StudentId == studentId)).ToList();
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedRepositoryResult<Assignment>(page, filtered.Count));
        }

        public Task<Assignment> AddAsync(Assignment assignment, CancellationToken cancellationToken = default)
        {
            Items.Add(assignment);
            return Task.FromResult(assignment);
        }

        public Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Assignment assignment, CancellationToken cancellationToken = default)
        {
            Items.Remove(assignment);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryIdempotencyRepository : IIdempotencyRepository
    {
        private readonly Dictionary<(string Scope, string Key), IdempotencyRecord> _records = [];

        public Task<IdempotencyRecord?> GetAsync(string scope, string key, CancellationToken cancellationToken) =>
            Task.FromResult(_records.GetValueOrDefault((scope, key)));

        public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken)
        {
            _records[(record.Scope, record.Key)] = record;
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
