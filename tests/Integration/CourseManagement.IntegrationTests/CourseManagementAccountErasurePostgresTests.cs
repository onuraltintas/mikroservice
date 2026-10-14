using System.Data.Common;
using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using CourseManagement.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementAccountErasurePostgresTests
{
    [Fact]
    public async Task Legal_hold_migration_blocks_assessments_created_before_verification_was_available()
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();

        try
        {
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .Options;
            var requestId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            await using var context = new CourseManagementDbContext(options);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261013085551_P0AccountErasure");
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "AccountErasureAssessments"
                    ("RequestId", "SubjectUserId", "CanProceed", "HasActiveLegalHold", "AssignmentCount",
                     "AttachmentCount", "SessionCount", "AgreementCount", "AffectedRecordCount", "RecordCountsJson", "AssessedAt")
                VALUES ({0}, {1}, TRUE, FALSE, 0, 0, 0, 0, 0, CAST({2} AS jsonb), {3});
                """,
                requestId, subjectId, "{}", DateTimeOffset.UtcNow);

            await migrator.MigrateAsync();

            var assessment = await context.ErasureAssessments.SingleAsync();
            Assert.False(assessment.CanProceed);
            Assert.False(assessment.LegalHoldStatusVerified);
            Assert.False(assessment.HasActiveLegalHold);
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(CourseManagementLegalHoldStatus.ActiveHold)]
    [InlineData(CourseManagementLegalHoldStatus.Unknown)]
    public async Task Account_erasure_rechecks_current_legal_hold_status_before_deleting_postgres_records(
        CourseManagementLegalHoldStatus executionLegalHoldStatus)
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();

        try
        {
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .Options;
            await using (var migrationContext = new CourseManagementDbContext(options))
                await migrationContext.Database.MigrateAsync();

            var subjectId = Guid.NewGuid();
            var workspace = Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "Kurum", WorkspaceType.Institution);
            var student = StudentRecord.Create(Guid.NewGuid(), workspace.Id, "Ada Öğrenci", null, null, DateTimeOffset.UtcNow);
            student.LinkAccount(subjectId);
            await using (var seed = new CourseManagementDbContext(options))
            {
                seed.Workspaces.Add(workspace);
                seed.Students.Add(student);
                seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Ada Öğrenci"));
                await seed.SaveChangesAsync();
            }

            var provider = new MutableLegalHoldStatusProvider(CourseManagementLegalHoldStatus.NoActiveHold);
            var requestId = Guid.NewGuid();
            await using (var assessmentContext = new CourseManagementDbContext(options))
            {
                var service = CreateService(assessmentContext, new TrackingAttachmentStorage(), provider);
                var assessment = await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);
                Assert.True(assessment.CanProceed);
            }

            provider.Status = executionLegalHoldStatus;
            await using (var executionContext = new CourseManagementDbContext(options))
            {
                var service = CreateService(executionContext, new TrackingAttachmentStorage(), provider);
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
                    ExecutionRequest(requestId, subjectId), CancellationToken.None));
            }

            await using var verification = new CourseManagementDbContext(options);
            Assert.Equal(subjectId, (await verification.Students.SingleAsync()).UserId);
            Assert.Contains(await verification.UserProfiles.ToListAsync(), profile => profile.UserId == subjectId);
            Assert.Single(await verification.ErasureAssessments.ToListAsync());
            Assert.Empty(await verification.ErasureExecutions.ToListAsync());
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task Account_erasure_anonymizes_postgres_records_and_preserves_read_only_sole_owner_workspace()
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();

        try
        {
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .Options;
            await using (var migrationContext = new CourseManagementDbContext(options))
                await migrationContext.Database.MigrateAsync();

            var subjectId = Guid.NewGuid();
            var institutionOwnerId = Guid.NewGuid();
            var institution = Workspace.Create(Guid.NewGuid(), institutionOwnerId, "Kurum", WorkspaceType.Institution);
            var independentWorkspace = Workspace.Create(Guid.NewGuid(), subjectId, "Özel Ders", WorkspaceType.IndependentTeacher);
            var student = StudentRecord.Create(Guid.NewGuid(), institution.Id, "Ada Öğrenci", "ada@example.test",
                "5551234567", DateTimeOffset.UtcNow);
            student.LinkAccount(subjectId);
            var course = Course.Create(Guid.NewGuid(), institution.Id, "Matematik", "Matematik", DateTimeOffset.UtcNow);
            var assignment = CourseAssignment.Create(Guid.NewGuid(), institution.Id, course.Id, null, student.Id,
                institutionOwnerId, "Kesirler", "Yanıtı yazın", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
            var submittedAt = DateTimeOffset.UtcNow.AddHours(-2);
            var reviewedAt = DateTimeOffset.UtcNow.AddHours(-1);
            var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), institution.Id, assignment.Id,
                student.Id, "Kişisel teslim metni", submittedAt);
            submission.Review(92, "Kişisel geri bildirim", subjectId, reviewedAt);
            var attachment = CourseAssignmentAttachment.Create(Guid.NewGuid(), institution.Id, assignment.Id,
                student.Id, submission.Id, CourseAssignmentAttachmentPurpose.StudentSubmission, "yanit.pdf",
                "application/pdf", 16, new string('A', 64), "student/answer.pdf", subjectId,
                DateTimeOffset.UtcNow.AddMinutes(-30), assignment.DueAt);
            var agreement = FeeAgreement.Create(Guid.NewGuid(), institution.Id, student.Id, course.Id,
                institutionOwnerId, FeeModel.MonthlyFixed, 500, null, null, null, DateTimeOffset.UtcNow);
            var installment = Installment.Create(Guid.NewGuid(), institution.Id, agreement.Id, student.Id,
                "Eylül taksidi", 500, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);
            var payment = Payment.Create(Guid.NewGuid(), institution.Id, student.Id, subjectId,
                PaymentMethod.Cash, 200, DateTimeOffset.UtcNow, "Kişisel ödeme notu");
            var teacherCourse = Course.Create(Guid.NewGuid(), independentWorkspace.Id, "İngilizce", "İngilizce",
                DateTimeOffset.UtcNow);
            var teacherGroup = CourseGroup.Create(Guid.NewGuid(), independentWorkspace.Id, teacherCourse.Id,
                subjectId, "Bire bir", DateTimeOffset.UtcNow);
            var teacherAssignment = CourseAssignment.Create(Guid.NewGuid(), independentWorkspace.Id, teacherCourse.Id,
                teacherGroup.Id, null, subjectId, "Telaffuz", null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
            var teacherMaterial = CourseAssignmentAttachment.Create(Guid.NewGuid(), independentWorkspace.Id,
                teacherAssignment.Id, null, null, CourseAssignmentAttachmentPurpose.TeacherMaterial, "worksheet.pdf",
                "application/pdf", 16, new string('B', 64), "teacher/worksheet.pdf", subjectId,
                DateTimeOffset.UtcNow, teacherAssignment.DueAt);

            await using (var seed = new CourseManagementDbContext(options))
            {
                seed.Workspaces.AddRange(institution, independentWorkspace);
                seed.UserProfiles.Add(CourseUserProfile.Create(subjectId, "Ada Öğrenci"));
                seed.Memberships.AddRange(
                    WorkspaceMembership.Create(institution.Id, institutionOwnerId, WorkspaceRole.Owner),
                    WorkspaceMembership.Create(institution.Id, subjectId, WorkspaceRole.Student),
                    WorkspaceMembership.Create(independentWorkspace.Id, subjectId, WorkspaceRole.Owner));
                seed.Students.Add(student);
                seed.Courses.AddRange(course, teacherCourse);
                seed.Assignments.AddRange(assignment, teacherAssignment);
                seed.AssignmentSubmissions.Add(submission);
                seed.AssignmentAttachments.AddRange(attachment, teacherMaterial);
                seed.FeeAgreements.Add(agreement);
                seed.Installments.Add(installment);
                seed.Payments.Add(payment);
                seed.Groups.Add(teacherGroup);
                await seed.SaveChangesAsync();
            }

            var storage = new TrackingAttachmentStorage("student/answer.pdf", "teacher/worksheet.pdf");
            var requestId = Guid.NewGuid();
            await using (var assessmentContext = new CourseManagementDbContext(options))
            {
                var service = CreateService(assessmentContext, storage);
                var assessment = await service.AssessAsync(AssessmentRequest(requestId, subjectId), CancellationToken.None);
                Assert.True(assessment.CanProceed);
                Assert.True(assessment.LegalHoldStatusVerified);
                Assert.Equal(1, assessment.RecordCounts["assignmentSubmissions"]);
                Assert.Equal(1, assessment.RecordCounts["attachments"]);
                Assert.Equal(1, assessment.RecordCounts["teacherMaterialsAnonymized"]);
            }

            var failingOptions = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .AddInterceptors(new FailOnCommitInterceptor())
                .Options;
            await using (var failingContext = new CourseManagementDbContext(failingOptions))
            {
                var service = CreateService(failingContext, storage);
                await Assert.ThrowsAsync<CommitFailureException>(() => service.ExecuteAsync(
                    ExecutionRequest(requestId, subjectId), CancellationToken.None));
            }

            await using (var verification = new CourseManagementDbContext(options))
            {
                Assert.Equal(subjectId, (await verification.Students.SingleAsync()).UserId);
                Assert.Equal("Kişisel teslim metni", (await verification.AssignmentSubmissions.SingleAsync()).Text);
                Assert.Equal(2, await verification.AssignmentAttachments.CountAsync());
                Assert.False((await verification.Workspaces.SingleAsync(item => item.Id == independentWorkspace.Id)).IsErasureReadOnly);
                Assert.True(await verification.ErasureAssessments.AnyAsync(item => item.RequestId == requestId));
                Assert.Empty(await verification.ErasureExecutions.ToListAsync());
                Assert.False(storage.Contains("student/answer.pdf"),
                    "a storage delete completed before the database commit error and is idempotent on retry");
                Assert.True(storage.Contains("teacher/worksheet.pdf"));
            }

            await using (var retryContext = new CourseManagementDbContext(options))
            {
                var service = CreateService(retryContext, storage);
                await service.ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);
            }

            await using (var verification = new CourseManagementDbContext(options))
            {
                var retainedWorkspace = await verification.Workspaces.SingleAsync(item => item.Id == independentWorkspace.Id);
                var anonymizedOwnerId = retainedWorkspace.OwnerUserId;
                var retainedStudent = await verification.Students.SingleAsync();
                Assert.Null(retainedStudent.UserId);
                Assert.Equal("Silinmiş öğrenci", retainedStudent.DisplayName);
                Assert.Null(retainedStudent.Email);
                Assert.Null(retainedStudent.PhoneNumber);

                var retainedSubmission = await verification.AssignmentSubmissions.SingleAsync();
                Assert.Null(retainedSubmission.Text);
                Assert.Null(retainedSubmission.Feedback);
                Assert.Equal(92, retainedSubmission.Score);
                Assert.Equal(submittedAt.UtcDateTime.Ticks / 10,
                    retainedSubmission.SubmittedAt.UtcDateTime.Ticks / 10);
                Assert.Equal(reviewedAt.UtcDateTime.Ticks / 10,
                    retainedSubmission.ReviewedAt!.Value.UtcDateTime.Ticks / 10);
                Assert.Equal(anonymizedOwnerId, retainedSubmission.ReviewedByUserId);
                var retainedMaterial = await verification.AssignmentAttachments.SingleAsync();
                Assert.Equal(teacherMaterial.Id, retainedMaterial.Id);
                Assert.Equal("worksheet.pdf", retainedMaterial.FileName);
                Assert.Equal(anonymizedOwnerId, retainedMaterial.UploadedByUserId);
                Assert.Equal(500, (await verification.Installments.SingleAsync()).Amount);
                Assert.Equal("Taksit", (await verification.Installments.SingleAsync()).Label);
                var retainedPayment = await verification.Payments.SingleAsync();
                Assert.Equal(200, retainedPayment.Amount);
                Assert.Null(retainedPayment.Note);
                Assert.Equal(anonymizedOwnerId, retainedPayment.RecordedByUserId);

                Assert.True(retainedWorkspace.IsErasureReadOnly);
                Assert.NotEqual(subjectId, retainedWorkspace.OwnerUserId);
                var anonymousOwner = await verification.Memberships.SingleAsync(item => item.WorkspaceId == independentWorkspace.Id);
                Assert.Equal(anonymizedOwnerId, anonymousOwner.UserId);
                Assert.Equal(WorkspaceRole.Owner, anonymousOwner.Role);
                Assert.False(anonymousOwner.IsActive);
                Assert.Contains(await verification.UserProfiles.ToListAsync(), profile =>
                    profile.UserId == anonymizedOwnerId && profile.DisplayName == "Silinmiş kullanıcı");
                Assert.NotEqual(subjectId, (await verification.Groups.SingleAsync()).TeacherUserId);
                Assert.DoesNotContain(await verification.Memberships.ToListAsync(), item => item.UserId == subjectId);
                Assert.DoesNotContain(await verification.UserProfiles.ToListAsync(), item => item.UserId == subjectId);
                Assert.Empty(await verification.ErasureAssessments.ToListAsync());
                Assert.Single(await verification.ErasureExecutions.ToListAsync());
            }

            Assert.Equal(new[] { "student/answer.pdf" }, storage.DeletedStorageKeys);
            Assert.True(storage.Contains("teacher/worksheet.pdf"));
            await using var idempotencyContext = new CourseManagementDbContext(options);
            var duplicateExecution = await CreateService(idempotencyContext, storage)
                .ExecuteAsync(ExecutionRequest(requestId, subjectId), CancellationToken.None);
            Assert.Equal(requestId, duplicateExecution.RequestId);
            Assert.Equal(new[] { "student/answer.pdf" }, storage.DeletedStorageKeys);
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }

    private static PersonalDataErasureAssessmentRequestedV1 AssessmentRequest(Guid requestId, Guid subjectId) =>
        new(Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, true, PersonalDataScope.Account);

    private static PersonalDataErasureExecutionRequestedV1 ExecutionRequest(Guid requestId, Guid subjectId) =>
        new(Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Account);

    private static CourseManagementAccountErasureService CreateService(CourseManagementDbContext context,
        ICourseAssignmentAttachmentStorage storage, ICourseManagementLegalHoldStatusProvider? legalHoldStatusProvider = null) =>
        new(context, storage, TimeProvider.System,
            legalHoldStatusProvider ?? new FixedLegalHoldStatusProvider(CourseManagementLegalHoldStatus.NoActiveHold));

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

        public Task<CourseManagementLegalHoldStatus> GetStatusAsync(Guid requestId, Guid subjectUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Status);
    }

    private sealed class TrackingAttachmentStorage : ICourseAssignmentAttachmentStorage
    {
        private readonly HashSet<string> _files;

        public TrackingAttachmentStorage(params string[] existingFiles) => _files = existingFiles.ToHashSet(StringComparer.Ordinal);

        public List<string> DeletedStorageKeys { get; } = [];
        public bool Contains(string storageKey) => _files.Contains(storageKey);

        public Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_files.Remove(storageKey)) DeletedStorageKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnCommitInterceptor : DbTransactionInterceptor
    {
        private int _shouldFail = 1;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _shouldFail, 0) == 1)
                throw new CommitFailureException();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitFailureException : Exception { }
}
