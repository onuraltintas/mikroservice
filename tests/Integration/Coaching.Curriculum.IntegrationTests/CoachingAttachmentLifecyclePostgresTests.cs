using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CreateAssignmentAttachment;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Application.Commands.DeleteAssignmentAttachment;
using Coaching.Application.Commands.UploadAssignmentAttachment;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

// Package 4 of claude/coaching-fix-plan.md: student and teacher files.
public sealed class CoachingAttachmentLifecyclePostgresTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly string PngHash = Convert.ToHexString(SHA256.HashData(Png));

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ScannerOutageLeavesTheUploadRetryableAndRemovesTheBytes()
    {
        await using var files = new Files(); await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg, files);
        var created = await world.CreateAttachment();

        var outage = await Assert.ThrowsAsync<BusinessRuleException>(() => world.Upload(created.AttachmentId, new Scanner(fail: true)));
        Assert.Equal("Attachment.ScannerUnavailable", outage.Code);
        pg.Db.ChangeTracker.Clear();
        var pending = await pg.Db.Set<AssignmentSubmissionAttachment>().SingleAsync();
        Assert.Equal(AttachmentScanStatus.PendingUpload, pending.Status);
        Assert.Empty(Directory.EnumerateFiles(files.Root, "*", SearchOption.AllDirectories));

        pg.Db.ChangeTracker.Clear();
        var retried = await world.Upload(created.AttachmentId, new Scanner());
        Assert.Equal("Clean", retried.Status);
        Assert.Single(Directory.EnumerateFiles(files.Root, "*", SearchOption.AllDirectories));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentFilesAreLimitedPerAssignmentAndCanBeRemovedUntilGraded()
    {
        await using var files = new Files(); await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg, files);
        var first = await world.CreateAttachment();
        for (var i = 1; i < AssignmentAttachmentPolicy.MaxSubmissionAttachmentsPerStudent; i++) await world.CreateAttachment();
        var limit = await Assert.ThrowsAsync<BusinessRuleException>(() => world.CreateAttachment());
        Assert.Equal("Attachment.LimitReached", limit.Code);

        pg.Db.ChangeTracker.Clear();
        await world.Upload(first.AttachmentId, new Scanner());
        pg.Db.ChangeTracker.Clear();
        await world.RemoveAttachment(first.AttachmentId);
        pg.Db.ChangeTracker.Clear();
        Assert.Equal(AssignmentAttachmentPolicy.MaxSubmissionAttachmentsPerStudent - 1, await pg.Db.Set<AssignmentSubmissionAttachment>().CountAsync());
        Assert.Empty(Directory.EnumerateFiles(files.Root, "*", SearchOption.AllDirectories));
        Assert.Contains(world.Events, e => e is AssignmentAttachmentDeletionRequested d && d.AttachmentId == first.AttachmentId);
        await world.CreateAttachment();

        pg.Db.ChangeTracker.Clear();
        var assignment = await pg.Db.Assignments.Include(x => x.AssignedStudents).SingleAsync();
        assignment.AssignedStudents.Single().Grade(90); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        var someId = (await pg.Db.Set<AssignmentSubmissionAttachment>().FirstAsync()).Id;
        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => world.RemoveAttachment(someId));
        Assert.Equal("Attachment.Locked", locked.Code);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CleanupRemovesOnlyIncompleteUploadsAndTheirBytes()
    {
        await using var files = new Files(); await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg, files);
        var expired = await world.CreateAttachment(); var open = await world.CreateAttachment(); var clean = await world.CreateAttachment();
        pg.Db.ChangeTracker.Clear(); await world.Upload(clean.AttachmentId, new Scanner());
        await File.WriteAllBytesAsync(Path.Combine(files.Root, "orphan-check"), [1]);
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.assignment_submission_attachments SET upload_expires_at = {DateTime.UtcNow.AddHours(-2)} WHERE id = {expired.AttachmentId}
            """);
        pg.Db.ChangeTracker.Clear();

        var removed = await new CoachingAttachmentCleanupProcessor(pg.Db, files.Storage, TimeProvider.System,
            NullLogger<CoachingAttachmentCleanupProcessor>.Instance).CleanupAsync(default);

        Assert.Equal(1, removed);
        var left = await pg.Db.Set<AssignmentSubmissionAttachment>().Select(x => x.Id).ToArrayAsync();
        Assert.Equal(new[] { open.AttachmentId, clean.AttachmentId }.Order(), left.Order());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DeletingAnAssignmentAlsoRemovesTeacherFiles()
    {
        await using var files = new Files(); await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg, files);
        var assignment = await pg.Db.Assignments.SingleAsync();
        var key = $"assignments/{assignment.Id:N}/instructions/{Guid.NewGuid():N}";
        await using (var content = new MemoryStream(Png)) await files.Storage.StoreAsync(key, content, "image/png", Png.Length, PngHash);
        assignment.AddInstructionAttachment(key, "kaynak.png", "image/png", Png.Length, PngHash, DateTime.UtcNow);
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        pg.Data.User.UserId = world.Teacher; pg.Data.User.Roles = ["Teacher"];

        await new DeleteAssignmentCommandHandler(new AssignmentRepository(pg.Db), new UnitOfWork(pg.Db), new CoachingAccessPolicy(pg.Data.User),
            files.Storage, world.Publisher, NullLogger<DeleteAssignmentCommandHandler>.Instance, world.Identity)
            .Handle(new DeleteAssignmentCommand(assignment.Id), default);

        Assert.Empty(Directory.EnumerateFiles(files.Root, "*", SearchOption.AllDirectories));
        Assert.Contains(world.Events, e => e is AssignmentAttachmentDeletionRequested d && d.StorageKey == key);
    }

    [Fact]
    public void AssignmentTextIsBoundedBeforeItReachesTheDatabase()
    {
        var validator = new CreateAssignmentCommandValidator();
        var valid = new CreateAssignmentCommand { TeacherId = Guid.NewGuid(), Title = "Ödev", Description = "Açıklama", Subject = "Matematik", DueDate = DateTime.UtcNow.AddDays(1) };
        Assert.DoesNotContain(validator.Validate(valid).Errors, e => e.PropertyName is "Description" or "Subject");
        Assert.Contains(validator.Validate(valid with { Description = new string('a', 20001) }).Errors, e => e.PropertyName == "Description");
        Assert.Contains(validator.Validate(valid with { Subject = new string('a', 101) }).Errors, e => e.PropertyName == "Subject");
    }

    private sealed class World
    {
        private readonly CurriculumPostgresTests.Pg pg; private readonly Files files;
        public Guid Teacher { get; } = Guid.NewGuid(); public Guid Student { get; } = Guid.NewGuid();
        public List<object> Events { get; } = [];
        public Publisher Publisher { get; }
        public Identity Identity { get; } = new();
        private World(CurriculumPostgresTests.Pg pg, Files files) { this.pg = pg; this.files = files; Publisher = new(Events); }
        public static async Task<World> Create(CurriculumPostgresTests.Pg pg, Files files)
        {
            var world = new World(pg, files);
            var assignment = Assignment.Create(world.Teacher, "Fotoğraflı ödev", DateTime.UtcNow.AddDays(3));
            assignment.AssignToStudent(world.Student);
            pg.Db.Add(assignment); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
            return world;
        }
        private CoachingAccessPolicy Student_() { pg.Data.User.UserId = Student; pg.Data.User.Roles = ["Student"]; return new(pg.Data.User); }
        private Guid AssignmentId => pg.Db.Assignments.AsNoTracking().Select(x => x.Id).Single();
        public async Task<CreateAssignmentAttachmentResponse> CreateAttachment()
        {
            var result = await new CreateAssignmentAttachmentCommandHandler(new AssignmentRepository(pg.Db), new UnitOfWork(pg.Db), Student_(), Identity, files.Storage)
                .Handle(new CreateAssignmentAttachmentCommand(AssignmentId, Student, "foto.png", "image/png", Png.Length, PngHash), default);
            pg.Db.ChangeTracker.Clear();
            return result;
        }
        public Task<UploadAssignmentAttachmentResponse> Upload(Guid attachment, IAssignmentAttachmentScanner scanner) =>
            new UploadAssignmentAttachmentCommandHandler(new AssignmentRepository(pg.Db), new UnitOfWork(pg.Db), Student_(), Identity, files.Storage, scanner)
                .Handle(new UploadAssignmentAttachmentCommand(AssignmentId, Student, attachment, new MemoryStream(Png), "image/png", Png.Length, PngHash), default);
        public Task RemoveAttachment(Guid attachment) =>
            new DeleteAssignmentAttachmentCommandHandler(new AssignmentRepository(pg.Db), new UnitOfWork(pg.Db), Student_(), Identity, files.Storage, Publisher)
                .Handle(new DeleteAssignmentAttachmentCommand(AssignmentId, Student, attachment), default);
    }

    private sealed class Files : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "coaching-attachments-" + Guid.NewGuid().ToString("N"));
        public LocalAssignmentAttachmentStorage Storage { get; }
        public Files() { Directory.CreateDirectory(Root); Storage = new(Options.Create(new AssignmentAttachmentOptions { RootPath = Root })); }
        public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }

    private sealed class Scanner(bool fail = false) : IAssignmentAttachmentScanner
    {
        public Task<AttachmentScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default) =>
            fail ? throw new IOException("clamd is not reachable") : Task.FromResult(new AttachmentScanResult(true));
    }

    internal sealed class Publisher(List<object> events) : ICoachingEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { events.Add(message); return Task.CompletedTask; }
    }

    internal sealed class Identity : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken cancellationToken)
            => Task.FromResult(requestedInstitutionId);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) => Task.FromResult(studentIds);
    }
}
