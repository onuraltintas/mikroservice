using CourseManagement.Domain;
using CourseManagement.Application.CourseAssignments;
using CourseManagement.Infrastructure;
using CourseManagement.Infrastructure.Attachments;
using CourseManagement.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseManagementAccountErasureTests
{
    [Fact]
    public async Task Assessment_contributes_its_local_result_then_unknown_identity_summary_blocks_execution()
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var provider = new MutableLegalHoldStatusProvider(CourseManagementLegalHoldStatus.Unknown);
        var service = CreateService(options, new TrackingAttachmentStorage(), provider);
        var requestId = Guid.NewGuid();

        var assessment = await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);

        Assert.True(assessment.CanProceed);
        Assert.False(assessment.HasActiveLegalHold);
        Assert.True(assessment.LegalHoldStatusVerified);
        Assert.Equal(0, provider.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            ExecutionRequest(requestId, subjectId), CancellationToken.None));
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(CourseManagementLegalHoldStatus.ActiveHold)]
    [InlineData(CourseManagementLegalHoldStatus.Unknown)]
    public async Task Execution_rechecks_legal_hold_status_before_erasure(
        CourseManagementLegalHoldStatus executionLegalHoldStatus)
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "Kurum", WorkspaceType.Institution);
        var student = StudentRecord.Create(Guid.NewGuid(), workspace.Id, "Ada Öğrenci", null, null, DateTimeOffset.UtcNow);
        student.LinkAccount(subjectId);
        var legalHoldProvider = new MutableLegalHoldStatusProvider(CourseManagementLegalHoldStatus.NoActiveHold);

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.Students.Add(student);
            seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Ada Öğrenci"));
            await seed.SaveChangesAsync();
        }

        var service = new CourseManagementAccountErasureService(
            new CourseManagementDbContext(options), new TrackingAttachmentStorage(), TimeProvider.System, legalHoldProvider);
        var requestId = Guid.NewGuid();
        var assessment = await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);
        Assert.True(assessment.CanProceed);

        legalHoldProvider.Status = executionLegalHoldStatus;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            ExecutionRequest(requestId, subjectId), CancellationToken.None));

        await using var verify = new CourseManagementDbContext(options);
        Assert.Equal(subjectId, (await verify.Students.SingleAsync()).UserId);
        Assert.Contains(await verify.UserProfiles.ToListAsync(), profile => profile.UserId == subjectId);
        Assert.Single(await verify.ErasureAssessments.ToListAsync());
        Assert.Empty(await verify.ErasureExecutions.ToListAsync());
    }

    [Fact]
    public async Task Teacher_erasure_keeps_teacher_material_and_anonymizes_its_uploader()
    {
        var options = CreateOptions();
        var teacherId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), ownerId, "Kurum", WorkspaceType.Institution);
        var course = Course.Create(Guid.NewGuid(), workspace.Id, "Matematik", "Matematik", DateTimeOffset.UtcNow);
        var group = CourseGroup.Create(Guid.NewGuid(), workspace.Id, course.Id, teacherId, "6-A", DateTimeOffset.UtcNow);
        var assignment = CourseAssignment.Create(Guid.NewGuid(), workspace.Id, course.Id, group.Id, null, teacherId,
            "Kesirler", null, DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow);
        var material = CourseAssignmentAttachment.Create(Guid.NewGuid(), workspace.Id, assignment.Id,
            null, null, CourseAssignmentAttachmentPurpose.TeacherMaterial, "calisma.pdf", "application/pdf", 16,
            new string('A', 64), "teacher/worksheet.pdf", teacherId, DateTimeOffset.UtcNow, assignment.DueAt);

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.UserProfiles.Add(CourseUserProfile.Create(teacherId, "Öğretmen"));
            seed.Memberships.Add(WorkspaceMembership.Create(workspace.Id, teacherId, WorkspaceRole.Teacher));
            seed.Courses.Add(course);
            seed.Groups.Add(group);
            seed.Assignments.Add(assignment);
            seed.AssignmentAttachments.Add(material);
            await seed.SaveChangesAsync();
        }

        var storage = new TrackingAttachmentStorage("teacher/worksheet.pdf");
        var service = CreateService(options, storage);
        var requestId = Guid.NewGuid();
        var assessment = await service.AssessAsync(AssessmentRequest(requestId, teacherId), CancellationToken.None);
        Assert.Equal(1, assessment.RecordCounts["teacherMaterialsAnonymized"]);

        await service.ExecuteAsync(ExecutionRequest(requestId, teacherId), CancellationToken.None);

        await using var verify = new CourseManagementDbContext(options);
        var retainedMaterial = await verify.AssignmentAttachments.SingleAsync();
        Assert.Equal(material.Id, retainedMaterial.Id);
        Assert.Equal("calisma.pdf", retainedMaterial.FileName);
        Assert.Equal("teacher/worksheet.pdf", retainedMaterial.StorageKey);
        Assert.NotEqual(teacherId, retainedMaterial.UploadedByUserId);
        Assert.Contains(retainedMaterial.UploadedByUserId, await verify.UserProfiles.Select(profile => profile.UserId).ToListAsync());
        Assert.Contains("teacher/worksheet.pdf", storage.Files);
    }

    [Fact]
    public async Task Student_erasure_removes_identity_and_submission_content_but_keeps_finance_and_grade_history()
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), ownerId, "Kurum", WorkspaceType.Institution);
        var student = StudentRecord.Create(Guid.NewGuid(), workspace.Id, "Ada Öğrenci", "ada@example.test", "5551234567", DateTimeOffset.UtcNow);
        student.LinkAccount(subjectId);
        var guardian = GuardianContact.Create(Guid.NewGuid(), workspace.Id, "Veli", "veli@example.test", "5559876543", DateTimeOffset.UtcNow);
        var guardianLink = StudentGuardianLink.Create(Guid.NewGuid(), workspace.Id, student.Id, guardian.Id,
            GuardianRelationship.Parent, DateTimeOffset.UtcNow);
        var course = Course.Create(Guid.NewGuid(), workspace.Id, "Matematik", "Matematik", DateTimeOffset.UtcNow);
        var assignment = CourseAssignment.Create(Guid.NewGuid(), workspace.Id, course.Id, null, student.Id, ownerId,
            "Kesirler", "Yanıtı yazın", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id,
            "Yanıt içeriği", DateTimeOffset.UtcNow.AddMinutes(-5));
        var reviewedAt = DateTimeOffset.UtcNow;
        submission.Review(92, "Öğretmen geri bildirimi", ownerId, reviewedAt);
        var attachment = CourseAssignmentAttachment.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id,
            submission.Id, CourseAssignmentAttachmentPurpose.StudentSubmission, "yanit.pdf", "application/pdf", 16,
            new string('A', 64), "student/answer.pdf", subjectId, DateTimeOffset.UtcNow, assignment.DueAt);
        var agreement = FeeAgreement.Create(Guid.NewGuid(), workspace.Id, student.Id, course.Id, ownerId,
            FeeModel.MonthlyFixed, 500, null, null, null, DateTimeOffset.UtcNow);
        var installment = Installment.Create(Guid.NewGuid(), workspace.Id, agreement.Id, student.Id,
            "Eylül taksidi", 500, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);
        var payment = Payment.Create(Guid.NewGuid(), workspace.Id, student.Id, ownerId, PaymentMethod.Cash,
            200, DateTimeOffset.UtcNow, "Tahsilat notu");
        var membership = WorkspaceMembership.Create(workspace.Id, subjectId, WorkspaceRole.Student);

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Ada Öğrenci"));
            seed.Memberships.Add(membership);
            seed.Students.Add(student);
            seed.Guardians.Add(guardian);
            seed.StudentGuardianLinks.Add(guardianLink);
            seed.Courses.Add(course);
            seed.Assignments.Add(assignment);
            seed.AssignmentSubmissions.Add(submission);
            seed.AssignmentAttachments.Add(attachment);
            seed.FeeAgreements.Add(agreement);
            seed.Installments.Add(installment);
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }

        var storage = new TrackingAttachmentStorage("student/answer.pdf");
        var service = CreateService(options, storage);
        var requestId = Guid.NewGuid();
        var assessment = await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);

        Assert.True(assessment.CanProceed);
        Assert.Equal(1, assessment.RecordCounts["students"]);
        Assert.Equal(1, assessment.RecordCounts["assignmentSubmissions"]);
        Assert.Equal(1, assessment.RecordCounts["attachments"]);

        var execution = await service.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);

        await using var verify = new CourseManagementDbContext(options);
        var anonymizedStudent = await verify.Students.SingleAsync();
        Assert.Null(anonymizedStudent.UserId);
        Assert.Equal("Silinmiş öğrenci", anonymizedStudent.DisplayName);
        Assert.Null(anonymizedStudent.Email);
        Assert.Null(anonymizedStudent.PhoneNumber);
        Assert.Empty(await verify.StudentGuardianLinks.ToListAsync());
        Assert.Empty(await verify.Guardians.ToListAsync());
        Assert.DoesNotContain(await verify.Memberships.ToListAsync(), item => item.UserId == subjectId);
        Assert.DoesNotContain(await verify.UserProfiles.ToListAsync(), item => item.UserId == subjectId);
        Assert.Equal("Silinmiş kullanıcı", (await verify.UserProfiles.SingleAsync()).DisplayName);

        var retainedSubmission = await verify.AssignmentSubmissions.SingleAsync();
        Assert.Null(retainedSubmission.Text);
        Assert.Null(retainedSubmission.Feedback);
        Assert.Equal(92, retainedSubmission.Score);
        Assert.Equal(submission.SubmittedAt, retainedSubmission.SubmittedAt);
        Assert.Equal(reviewedAt, retainedSubmission.ReviewedAt);
        Assert.Empty(await verify.AssignmentAttachments.ToListAsync());
        Assert.DoesNotContain("student/answer.pdf", storage.Files);
        Assert.Equal(500, (await verify.Installments.SingleAsync()).Amount);
        Assert.Equal(200, (await verify.Payments.SingleAsync()).Amount);
        Assert.Null((await verify.Payments.SingleAsync()).Note);
        Assert.Empty(await verify.ErasureAssessments.ToListAsync());
        Assert.Equal(requestId, execution.RequestId);
        Assert.True(execution.DeletedRecordCount > 0);
    }

    [Fact]
    public async Task Erasure_partial_storage_failure_preserves_database_state_and_allows_retry()
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), ownerId, "Kurum", WorkspaceType.Institution);
        var student = StudentRecord.Create(Guid.NewGuid(), workspace.Id, "Ada Öğrenci", null, null, DateTimeOffset.UtcNow);
        student.LinkAccount(subjectId);
        var course = Course.Create(Guid.NewGuid(), workspace.Id, "Matematik", "Matematik", DateTimeOffset.UtcNow);
        var assignment = CourseAssignment.Create(Guid.NewGuid(), workspace.Id, course.Id, null, student.Id, ownerId,
            "Kesirler", null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id,
            "Yanıt içeriği", DateTimeOffset.UtcNow);
        var attachments = new[]
        {
            CourseAssignmentAttachment.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id,
                submission.Id, CourseAssignmentAttachmentPurpose.StudentSubmission, "yanit-a.pdf", "application/pdf", 16,
                new string('A', 64), "student/answer-a.pdf", subjectId, DateTimeOffset.UtcNow, assignment.DueAt),
            CourseAssignmentAttachment.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id,
                submission.Id, CourseAssignmentAttachmentPurpose.StudentSubmission, "yanit-b.pdf", "application/pdf", 16,
                new string('B', 64), "student/answer-b.pdf", subjectId, DateTimeOffset.UtcNow, assignment.DueAt)
        };

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.Memberships.Add(WorkspaceMembership.Create(workspace.Id, subjectId, WorkspaceRole.Student));
            seed.Students.Add(student);
            seed.Courses.Add(course);
            seed.Assignments.Add(assignment);
            seed.AssignmentSubmissions.Add(submission);
            seed.AssignmentAttachments.AddRange(attachments);
            await seed.SaveChangesAsync();
        }

        var requestId = Guid.NewGuid();
        var failingStorage = new FailingOnSecondAttachmentStorage("student/answer-a.pdf", "student/answer-b.pdf");
        var failingService = CreateService(options, failingStorage);
        await failingService.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => failingService.ExecuteAsync(
            ExecutionRequest(requestId, subjectId), CancellationToken.None));

        await using (var verifyFailure = new CourseManagementDbContext(options))
        {
            Assert.Equal(subjectId, (await verifyFailure.Students.SingleAsync()).UserId);
            Assert.Equal(2, await verifyFailure.AssignmentAttachments.CountAsync());
            Assert.Single(await verifyFailure.ErasureAssessments.ToListAsync());
            Assert.Empty(await verifyFailure.ErasureExecutions.ToListAsync());
        }
        Assert.Single(failingStorage.DeletedStorageKeys);
        Assert.Single(failingStorage.Files);

        var retryStorage = new TrackingAttachmentStorage(failingStorage.Files.ToArray());
        var retryService = CreateService(options, retryStorage);
        var execution = await retryService.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);

        await using var verifyRetry = new CourseManagementDbContext(options);
        Assert.Null((await verifyRetry.Students.SingleAsync()).UserId);
        Assert.Empty(await verifyRetry.AssignmentAttachments.ToListAsync());
        Assert.Empty(await verifyRetry.ErasureAssessments.ToListAsync());
        Assert.Equal(requestId, (await verifyRetry.ErasureExecutions.SingleAsync()).RequestId);
        Assert.Equal(requestId, execution.RequestId);
        Assert.DoesNotContain("student/answer-a.pdf", retryStorage.Files);
        Assert.DoesNotContain("student/answer-b.pdf", retryStorage.Files);
    }

    [Fact]
    public async Task Erasing_the_only_workspace_owner_keeps_an_anonymized_owner_and_freezes_workspace()
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), subjectId, "Özel Ders", WorkspaceType.IndependentTeacher);
        var course = Course.Create(Guid.NewGuid(), workspace.Id, "İngilizce", "İngilizce", DateTimeOffset.UtcNow);
        var group = CourseGroup.Create(Guid.NewGuid(), workspace.Id, course.Id, subjectId, "Bire bir", DateTimeOffset.UtcNow);
        var ownerMembership = WorkspaceMembership.Create(workspace.Id, subjectId, WorkspaceRole.Owner);

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Öğretmen"));
            seed.Memberships.Add(ownerMembership);
            seed.Courses.Add(course);
            seed.Groups.Add(group);
            await seed.SaveChangesAsync();
        }

        var service = CreateService(options, new TrackingAttachmentStorage());
        var requestId = Guid.NewGuid();
        await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);
        var execution = await service.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);
        await using var verify = new CourseManagementDbContext(options);
        var retainedWorkspace = await verify.Workspaces.SingleAsync();
        var anonymousOwner = await verify.Memberships.SingleAsync();

        Assert.True(retainedWorkspace.IsErasureReadOnly);
        Assert.NotEqual(subjectId, retainedWorkspace.OwnerUserId);
        Assert.Equal(retainedWorkspace.OwnerUserId, anonymousOwner.UserId);
        Assert.Equal(WorkspaceRole.Owner, anonymousOwner.Role);
        Assert.False(anonymousOwner.IsActive);
        Assert.Contains(await verify.UserProfiles.ToListAsync(), profile =>
            profile.UserId == retainedWorkspace.OwnerUserId && profile.DisplayName == "Silinmiş kullanıcı");
        Assert.NotEqual(subjectId, (await verify.Groups.SingleAsync()).TeacherUserId);
        Assert.Equal(execution.DeletedRecordCount,
            (await service.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None)).DeletedRecordCount);
        Assert.Equal(retainedWorkspace.OwnerUserId, (await verify.Workspaces.SingleAsync()).OwnerUserId);
    }

    [Fact]
    public async Task Erasing_workspace_owner_transfers_workspace_to_another_active_owner()
    {
        var options = CreateOptions();
        var subjectId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), subjectId, "Kurum", WorkspaceType.Institution);

        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.Workspaces.Add(workspace);
            seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Silinecek sahip"));
            seed.Memberships.Add(WorkspaceMembership.Create(workspace.Id, subjectId, WorkspaceRole.Owner));
            seed.Memberships.Add(WorkspaceMembership.Create(workspace.Id, otherOwnerId, WorkspaceRole.Owner));
            await seed.SaveChangesAsync();
        }

        var service = CreateService(options, new TrackingAttachmentStorage());
        var requestId = Guid.NewGuid();
        await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);
        await service.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);

        await using var verify = new CourseManagementDbContext(options);
        var retainedWorkspace = await verify.Workspaces.SingleAsync();
        Assert.Equal(otherOwnerId, retainedWorkspace.OwnerUserId);
        Assert.False(retainedWorkspace.IsErasureReadOnly);
        Assert.DoesNotContain(await verify.Memberships.ToListAsync(), item => item.UserId == subjectId);
        var retainedMembership = Assert.Single(await verify.Memberships.ToListAsync());
        Assert.Equal(otherOwnerId, retainedMembership.UserId);
        Assert.Equal(WorkspaceRole.Owner, retainedMembership.Role);
        Assert.True(retainedMembership.IsActive);
    }

    private static DbContextOptions<CourseManagementDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<CourseManagementDbContext>()
            .UseInMemoryDatabase($"course-account-erasure-{Guid.NewGuid():N}")
            .Options;

    private static CourseManagementAccountErasureService CreateService(
        DbContextOptions<CourseManagementDbContext> options, ICourseAssignmentAttachmentStorage storage,
        ICourseManagementLegalHoldStatusProvider? legalHoldStatusProvider = null) =>
        new(new CourseManagementDbContext(options), storage, TimeProvider.System,
            legalHoldStatusProvider ?? new FixedLegalHoldStatusProvider(CourseManagementLegalHoldStatus.NoActiveHold));

    private static PersonalDataErasureAssessmentRequestedV1 AssessmentRequest(Guid requestId, Guid subjectId) =>
        new(Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, true, PersonalDataScope.Account);

    private static PersonalDataErasureExecutionRequestedV1 ExecutionRequest(Guid requestId, Guid subjectId) =>
        new(Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Account);

    private sealed class FixedLegalHoldStatusProvider(CourseManagementLegalHoldStatus status)
        : ICourseManagementLegalHoldStatusProvider
    {
        public Task<CourseManagementLegalHoldStatus> GetStatusAsync(Guid requestId, Guid subjectUserId, CancellationToken cancellationToken) =>
            Task.FromResult(status);
    }

    private sealed class MutableLegalHoldStatusProvider(CourseManagementLegalHoldStatus status)
        : ICourseManagementLegalHoldStatusProvider
    {
        public CourseManagementLegalHoldStatus Status { get; set; } = status;
        public int Calls { get; private set; }

        public Task<CourseManagementLegalHoldStatus> GetStatusAsync(Guid requestId, Guid subjectUserId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Status);
        }
    }

    private sealed class TrackingAttachmentStorage(params string[] storageKeys) : ICourseAssignmentAttachmentStorage
    {
        private readonly HashSet<string> _files = storageKeys.ToHashSet(StringComparer.Ordinal);
        public IReadOnlyCollection<string> Files => _files;
        public Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOnSecondAttachmentStorage(params string[] storageKeys) : ICourseAssignmentAttachmentStorage
    {
        private readonly HashSet<string> _files = storageKeys.ToHashSet(StringComparer.Ordinal);
        private readonly List<string> _deletedStorageKeys = [];
        private int _deleteCalls;

        public IReadOnlyCollection<string> Files => _files;
        public IReadOnlyCollection<string> DeletedStorageKeys => _deletedStorageKeys;
        public Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (++_deleteCalls == 2) throw new IOException("Attachment storage unavailable.");
            _files.Remove(storageKey);
            _deletedStorageKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }
}
