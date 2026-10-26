using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingErasureExecutionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_ShouldIncludeReflectionReviewsInStudentAndTeacherErasure(bool eraseTeacher)
    {
        await using var context=CreateContext();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var reflection=WeeklyReflection.Create(student,new(2026,10,12));reflection.Edit("Özel değerlendirme","","Tekrar",4);
        context.Add(reflection);await context.SaveChangesAsync();context.Add(WeeklyReflectionReview.Create(reflection,teacher,"Özel takip notu"));
        var other=WeeklyReflection.Create(Guid.NewGuid(),new(2026,10,12));other.Edit("Diğer öğrenci","","Tekrar",3);
        context.Add(other);await context.SaveChangesAsync();context.Add(WeeklyReflectionReview.Create(other,Guid.NewGuid(),"Diğer öğretmenin notu"));await context.SaveChangesAsync();
        var request=Request(eraseTeacher?teacher:student);
        await new CoachingErasureAssessmentService(context,TimeProvider.System).AssessAsync(AssessmentRequest(request),default);
        await new CoachingErasureExecutionService(context,new StubAttachmentStorage(),TimeProvider.System).ExecuteAsync(request,default);
        Assert.False(await context.Set<WeeklyReflectionReview>().AnyAsync(x=>x.StudentId==student));
        Assert.True(await context.Set<WeeklyReflectionReview>().AnyAsync(x=>x.StudentId==other.StudentId));
        Assert.Equal(eraseTeacher,await context.Set<WeeklyReflection>().AnyAsync(x=>x.StudentId==student));
    }

    [Fact]
    public async Task Execute_ShouldDeleteOnlySubjectDataAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(), "Ödev", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(subjectId);
        assignment.AssignedStudents.Single().AddSubmissionAttachment(
            "coaching/user-file.pdf", "ödev.pdf", "application/pdf", 128, new string('a', 64));
        context.Add(Coaching.Domain.Profiles.CoachingStudentProfile.Create(subjectId));
        context.Add(Coaching.Domain.Profiles.CoachingStudentProfile.Create(otherStudentId));
        context.Add(StudentLearningContext.Create(subjectId, 2026, LearningPurpose.General, null, null, null));
        context.Add(StudentLearningContext.Create(otherStudentId, 2026, LearningPurpose.General, null, null, null));
        context.Assignments.Add(assignment);
        context.AcademicGoals.AddRange(
            AcademicGoal.Create(subjectId, "Silinecek hedef", GoalCategory.SubjectMastery),
            AcademicGoal.Create(otherStudentId, "Korunacak hedef", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        context.AddRange(
            StudentPrimaryGoal.Create(subjectId, context.AcademicGoals.Local.Single(x => x.StudentId == subjectId).Id),
            StudentPrimaryGoal.Create(otherStudentId, context.AcademicGoals.Local.Single(x => x.StudentId == otherStudentId).Id));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(AssessmentRequest(request), CancellationToken.None);
        var storage = new StubAttachmentStorage();
        var service = new CoachingErasureExecutionService(context, storage, TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(6);
        (await context.Set<StudentPrimaryGoal>().CountAsync(x => x.StudentId == subjectId)).Should().Be(0);
        (await context.Set<StudentPrimaryGoal>().CountAsync(x => x.StudentId == otherStudentId)).Should().Be(1);
        (await context.StudentLearningContexts.CountAsync(x => x.StudentUserId == subjectId)).Should().Be(0);
        (await context.StudentLearningContexts.CountAsync(x => x.StudentUserId == otherStudentId)).Should().Be(1);
        (await context.CoachingStudentProfiles.CountAsync(x => x.UserId == subjectId)).Should().Be(0);
        (await context.CoachingStudentProfiles.CountAsync(x => x.UserId == otherStudentId)).Should().Be(1);
        replay.Id.Should().Be(first.Id);
        storage.DeletedKeys.Should().Equal("coaching/user-file.pdf");
        (await context.AssignmentStudents.CountAsync(row => row.StudentId == subjectId)).Should().Be(0);
        (await context.AssignmentSubmissionAttachments.CountAsync()).Should().Be(0);
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == subjectId)).Should().Be(0);
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == otherStudentId)).Should().Be(1);
        (await context.CoachingErasureExecutions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_ShouldRejectRequestWithoutCompletedAssessment()
    {
        await using var context = CreateContext();
        var service = new CoachingErasureExecutionService(context, new StubAttachmentStorage(), TimeProvider.System);

        var action = () => service.ExecuteAsync(Request(Guid.NewGuid()), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assessment*");
    }

    [Fact]
    public async Task Execute_ShouldHandFileDeletionToTheOutboxSoAStorageOutageDoesNotBlockErasure()
    {
        // Files are deleted by AssignmentAttachmentDeletionRequested after the erasure commits; that message is
        // retried on its own, so an unavailable object store neither blocks nor half-applies the erasure.
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Silinecek ödev", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(subjectId);
        assignment.AssignedStudents.Single().AddSubmissionAttachment(
            "coaching/retry-file.pdf", "ödev.pdf", "application/pdf", 128, new string('a', 64));
        context.Assignments.Add(assignment);
        context.AcademicGoals.Add(AcademicGoal.Create(subjectId, "Silinecek hedef", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(AssessmentRequest(request), CancellationToken.None);
        var storage = new StubAttachmentStorage { FailDelete = true };
        var outbox = new CapturingPublisher();

        var completed = await new CoachingErasureExecutionService(context, storage, TimeProvider.System, outbox)
            .ExecuteAsync(request, CancellationToken.None);

        completed.DeletedRecordCount.Should().Be(3);
        storage.DeletedKeys.Should().BeEmpty();
        outbox.Messages.OfType<AssignmentAttachmentDeletionRequested>().Select(x => x.StorageKey)
            .Should().Equal("coaching/retry-file.pdf");
        (await context.AssignmentStudents.CountAsync(row => row.StudentId == subjectId)).Should().Be(0);
        (await context.AssignmentSubmissionAttachments.CountAsync()).Should().Be(0);
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == subjectId)).Should().Be(0);
    }

    [Fact]
    public async Task Execute_ShouldRecheckLegalHoldImmediatelyBeforeDeletion()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        context.AcademicGoals.Add(AcademicGoal.Create(subjectId, "Korunacak hedef", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(AssessmentRequest(request), CancellationToken.None);
        context.CoachingLegalHolds.Add(CoachingLegalHold.Place(
            subjectId, "Yeni hukuki saklama", Guid.NewGuid(), DateTime.UtcNow));
        await context.SaveChangesAsync();

        var action = () => new CoachingErasureExecutionService(
            context, new StubAttachmentStorage(), TimeProvider.System)
            .ExecuteAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*legal hold*");
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == subjectId)).Should().Be(1);
    }

    private static PersonalDataErasureExecutionRequestedV1 Request(Guid subjectId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureExecutionRequestedV1(
            requestId, requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Coaching);
    }

    private static PersonalDataErasureAssessmentRequestedV1 AssessmentRequest(
        PersonalDataErasureExecutionRequestedV1 request) =>
        new(request.EventId, request.RequestId, request.SubjectUserId, request.AuthorizedAt,
            DryRun: true, request.Scope);

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }

    private sealed class CapturingPublisher : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubAttachmentStorage : IAssignmentAttachmentStorage
    {
        public List<string> DeletedKeys { get; } = [];
        public bool FailDelete { get; set; }

        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(
            Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredAssignmentAttachment> StoreAsync(
            string storageKey, Stream content, string expectedContentType, long expectedSizeBytes,
            string expectedSha256, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (FailDelete)
                throw new IOException("Disposable object storage is unavailable.");
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }
}
