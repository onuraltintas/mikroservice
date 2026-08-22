using System.Text;
using Coaching.Application;
using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetAssignmentAttachment;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAttachmentReadTests
{
    [Fact]
    public async Task CleanAttachment_IsReturnedOnlyForAnAuthorizedStudentScope()
    {
        var assignment = CreateAssignmentWithAttachment(AttachmentScanStatus.Clean, out var attachment);
        var studentId = assignment.AssignedStudents.Single().StudentId;
        var storage = new FakeStorage(Encoding.UTF8.GetBytes("photo"));
        var handler = CreateHandler(assignment, studentId, storage);

        var result = await handler.Handle(
            new GetAssignmentAttachmentQuery(assignment.Id, studentId, attachment.Id),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.OriginalFileName.Should().Be("photo.jpg");
        using var reader = new StreamReader(result.Content);
        (await reader.ReadToEndAsync()).Should().Be("photo");
    }

    [Fact]
    public async Task PendingAttachment_CannotBeDownloaded()
    {
        var assignment = CreateAssignmentWithAttachment(AttachmentScanStatus.PendingScan, out var attachment);
        var studentId = assignment.AssignedStudents.Single().StudentId;
        var handler = CreateHandler(assignment, studentId, new FakeStorage([]));

        var act = () => handler.Handle(
            new GetAssignmentAttachmentQuery(assignment.Id, studentId, attachment.Id),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("Attachment.NotAvailable");
    }

    private static GetAssignmentAttachmentQueryHandler CreateHandler(
        Assignment assignment,
        Guid viewerId,
        FakeStorage storage)
    {
        return new GetAssignmentAttachmentQueryHandler(
            new FakeAssignmentRepository(assignment),
            new FakeAccessPolicy(viewerId),
            new FakeIdentityAuthorizationClient(viewerId),
            storage);
    }

    private static Assignment CreateAssignmentWithAttachment(
        AttachmentScanStatus status,
        out AssignmentSubmissionAttachment attachment)
    {
        var assignment = Assignment.Create(Guid.NewGuid(), "Kitap ödevi", DateTime.UtcNow.AddDays(1));
        var studentId = Guid.NewGuid();
        assignment.AssignToStudent(studentId);
        var student = assignment.AssignedStudents.Single();
        attachment = student.AddSubmissionAttachment(
            "assignments/test/photo-1",
            "photo.jpg",
            "image/jpeg",
            5,
            new string('A', 64));
        attachment.SetUploadExpiry(DateTime.UtcNow.AddMinutes(5));
        attachment.MarkUploaded();
        if (status == AttachmentScanStatus.Clean)
            attachment.MarkClean();
        else if (status == AttachmentScanStatus.Rejected)
            attachment.Reject("test rejection");
        return assignment;
    }

    private sealed class FakeAssignmentRepository(Assignment assignment) : IAssignmentRepository
    {
        public Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Assignment?>(id == assignment.Id ? assignment : null);
        public Task<PagedRepositoryResult<Assignment>> GetByTeacherIdAsync(Guid id, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedRepositoryResult<Assignment>> GetByStudentIdAsync(Guid id, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Assignment> AddAsync(Assignment value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Assignment value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Assignment value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeAccessPolicy(Guid viewerId) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => viewerId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid studentId) => studentId == viewerId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrStudent(Guid teacherId, Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }

    private sealed class FakeIdentityAuthorizationClient(Guid viewerId) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(viewerUserId == viewerId ? studentIds : []);
    }

    private sealed class FakeStorage(byte[] bytes) : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string storageKey, Stream content, string expectedContentType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
