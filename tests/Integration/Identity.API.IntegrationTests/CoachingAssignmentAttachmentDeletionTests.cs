using Coaching.Application.Authorization;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAssignmentAttachmentDeletionTests
{
    [Fact]
    public async Task DatabaseFailure_LeavesPhysicalAttachmentAvailableForRetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "coaching-delete-db-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var context = new CoachingDbContext(
                new DbContextOptionsBuilder<CoachingDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options);
            var teacherId = Guid.NewGuid();
            var assignment = Assignment.Create(teacherId, "Test assignment", DateTime.UtcNow.AddDays(1));
            assignment.AssignToStudent(Guid.NewGuid());
            assignment.AssignedStudents.Single().AddSubmissionAttachment(
                "assignments/test/photo.jpg", "photo.jpg", "image/jpeg", 3, new string('A', 64));
            context.Assignments.Add(assignment);
            await context.SaveChangesAsync();
            var path = Path.Combine(root, "assignments", "test", "photo.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [1, 2, 3]);

            var handler = new DeleteAssignmentCommandHandler(
                new AssignmentRepository(context),
                new FailingUnitOfWork(),
                new TeacherAccessPolicy(teacherId),
                new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })),
                new RecordingPublisher(),
                NullLogger<DeleteAssignmentCommandHandler>.Instance,
                new AllowTeacherRecords());

            var act = () => handler.Handle(new DeleteAssignmentCommand(assignment.Id), CancellationToken.None);

            await act.Should().ThrowAsync<IOException>();
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StorageFailure_LeavesCommittedDeletionForOutboxRetry()
    {
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        var teacherId = Guid.NewGuid();
        var assignment = Assignment.Create(teacherId, "Test assignment", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(Guid.NewGuid());
        assignment.AssignedStudents.Single().AddSubmissionAttachment(
            "assignments/test/photo.jpg", "photo.jpg", "image/jpeg", 3, new string('A', 64));
        context.Assignments.Add(assignment);
        await context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        var handler = new DeleteAssignmentCommandHandler(
            new AssignmentRepository(context),
            new UnitOfWork(context),
            new TeacherAccessPolicy(teacherId),
            new FailingStorage(),
            publisher,
            NullLogger<DeleteAssignmentCommandHandler>.Instance,
                new AllowTeacherRecords());

        var act = () => handler.Handle(new DeleteAssignmentCommand(assignment.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await context.Assignments.CountAsync()).Should().Be(0);
        publisher.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task DeletingAssignment_RemovesItsPhysicalAttachment()
    {
        var root = Path.Combine(Path.GetTempPath(), "coaching-delete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var context = new CoachingDbContext(
                new DbContextOptionsBuilder<CoachingDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options);
            var teacherId = Guid.NewGuid();
            var assignment = Assignment.Create(teacherId, "Test assignment", DateTime.UtcNow.AddDays(1));
            assignment.AssignToStudent(Guid.NewGuid());
            const string key = "assignments/test/photo.jpg";
            assignment.AssignedStudents.Single().AddSubmissionAttachment(
                key, "photo.jpg", "image/jpeg", 3, new string('A', 64));
            assignment.AssignedStudents.Single().AddSubmissionAttachment(
                "assignments/test/second.jpg", "second.jpg", "image/jpeg", 3, new string('B', 64));
            context.Assignments.Add(assignment);
            await context.SaveChangesAsync();

            var path = Path.Combine(root, "assignments", "test", "photo.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [1, 2, 3]);
            var secondPath = Path.Combine(root, "assignments", "test", "second.jpg");
            await File.WriteAllBytesAsync(secondPath, [4, 5, 6]);
            var unrelatedPath = Path.Combine(root, "unrelated.jpg");
            await File.WriteAllBytesAsync(unrelatedPath, [7, 8, 9]);

            var publisher = new RecordingPublisher();
            var handler = new DeleteAssignmentCommandHandler(
                new AssignmentRepository(context),
                new UnitOfWork(context),
                new TeacherAccessPolicy(teacherId),
                new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })),
                publisher,
                NullLogger<DeleteAssignmentCommandHandler>.Instance,
                new AllowTeacherRecords());

            await handler.Handle(new DeleteAssignmentCommand(assignment.Id), CancellationToken.None);

            File.Exists(path).Should().BeFalse();
            File.Exists(secondPath).Should().BeFalse();
            File.Exists(unrelatedPath).Should().BeTrue();
            (await context.Assignments.CountAsync()).Should().Be(0);
            publisher.Messages.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SubmittedAssignmentsAreCancelledNotDeletedAndStudentsAreToldEitherWay()
    {
        await using var context = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var teacherId = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var submitted = Assignment.Create(teacherId, "Teslim alınan ödev", DateTime.UtcNow.AddDays(1));
        submitted.AssignToStudent(first); submitted.AssignToStudent(second); submitted.SubmitAssignment(first, "Bitti");
        var untouched = Assignment.Create(teacherId, "Henüz teslim yok", DateTime.UtcNow.AddDays(1)); untouched.AssignToStudent(second);
        context.Assignments.AddRange(submitted, untouched); await context.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        var delete = new DeleteAssignmentCommandHandler(new AssignmentRepository(context), new UnitOfWork(context), new TeacherAccessPolicy(teacherId),
            new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = Path.GetTempPath() })),
            publisher, NullLogger<DeleteAssignmentCommandHandler>.Instance, new AllowTeacherRecords());

        var refused = await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => delete.Handle(new DeleteAssignmentCommand(submitted.Id), default));
        refused.Code.Should().Be("Assignment.HasSubmissions");
        (await context.Assignments.CountAsync()).Should().Be(2);

        await delete.Handle(new DeleteAssignmentCommand(untouched.Id), default);
        publisher.All.OfType<EduPlatform.Shared.Contracts.Events.Coaching.AssignmentCancelledEvent>()
            .Should().ContainSingle(x => x.AssignmentId == untouched.Id && x.StudentIds.SequenceEqual(new[] { second }) && x.Title == "Henüz teslim yok");

        var cancel = new Coaching.Application.Commands.CancelAssignment.CancelAssignmentCommandHandler(new AssignmentRepository(context), new UnitOfWork(context),
            new TeacherAccessPolicy(teacherId), new AllowTeacherRecords(), publisher);
        await cancel.Handle(new Coaching.Application.Commands.CancelAssignment.CancelAssignmentCommand(submitted.Id), default);
        (await context.Assignments.SingleAsync()).Status.Should().Be(Coaching.Domain.Enums.AssignmentStatus.Cancelled);
        publisher.All.OfType<EduPlatform.Shared.Contracts.Events.Coaching.AssignmentCancelledEvent>()
            .Should().Contain(x => x.AssignmentId == submitted.Id && x.StudentIds.Length == 2);
    }

    private sealed class TeacherAccessPolicy(Guid teacherId) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => teacherId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid id) => id == teacherId;
        public bool IsCurrentStudent(Guid id) => false;
        public Guid RequireCurrentTeacher() => teacherId;
        public void RequireTeacher(Guid id)
        {
            if (id != teacherId) throw new UnauthorizedAccessException();
        }
        public void RequireStudent(Guid id) => throw new NotSupportedException();
        public void RequireTeacherOrStudent(Guid teacher, Guid student) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacher, IEnumerable<Guid> students) => throw new NotSupportedException();
    }

    private sealed class FailingStorage : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string storageKey, Stream content, string expectedContentType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new IOException("Storage unavailable");
    }

    private sealed class FailingUnitOfWork : Coaching.Application.Interfaces.IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new IOException("Database unavailable");
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPublisher : ICoachingEventPublisher
    {
        public List<AssignmentAttachmentDeletionRequested> Messages { get; } = [];
        public List<object> All { get; } = [];

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : class
        {
            All.Add(message);
            if (message is AssignmentAttachmentDeletionRequested deletion)
                Messages.Add(deletion);
            return Task.CompletedTask;
        }
    }

    // The teacher still teaches the assignment's students; these tests are about attachment cleanup.
    private sealed class AllowTeacherRecords : Coaching.Application.Interfaces.ICoachingIdentityAuthorizationClient
    {
        public Task<Coaching.Application.Interfaces.CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<Coaching.Application.Interfaces.CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId,
            bool isSystemAdministrator, CancellationToken cancellationToken) => Task.FromResult(requestedInstitutionId);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) =>
            Task.FromResult(studentIds);
    }
}
