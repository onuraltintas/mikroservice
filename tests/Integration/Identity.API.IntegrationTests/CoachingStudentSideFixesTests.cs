using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

// K4 (koçluk incelemesi 31.10.2026): student-side submission rules.
public sealed class CoachingStudentSideFixesTests
{
    private static (Assignment Assignment, AssignmentStudent Work, Guid Student) Work()
    {
        var student = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Paragraf", DateTime.UtcNow.AddDays(2));
        assignment.AssignToStudent(student);
        return (assignment, assignment.AssignedStudents.Single(), student);
    }

    private static AssignmentSubmissionAttachment File(AssignmentStudent work, string name = "foto.jpg")
    {
        var file = work.AddSubmissionAttachment("key/" + Guid.NewGuid(), name, "image/jpeg", 1000, new string('a', 64));
        file.SetUploadExpiry(DateTime.UtcNow.AddMinutes(30));
        return file;
    }

    [Fact]
    public void ARejectedFileOrAnAbandonedUploadDoesNotBlockTheSubmission()
    {
        var (assignment, work, student) = Work();
        var clean = File(work); clean.MarkUploaded(); clean.MarkClean();
        var rejected = File(work, "virus.jpg"); rejected.MarkUploaded(); rejected.Reject("Zararlı içerik");
        var abandoned = File(work, "yarim.jpg"); abandoned.SetUploadExpiry(DateTime.UtcNow.AddMinutes(-1));

        assignment.SubmitAssignment(student, "Bitti");

        work.Status.Should().Be(StudentAssignmentStatus.Submitted);
    }

    [Fact]
    public void AFileStillUploadingOrBeingScannedStopsTheSubmissionWithATurkishMessage()
    {
        var (assignment, work, student) = Work();
        var scanning = File(work); scanning.MarkUploaded();

        var error = Assert.Throws<BusinessRuleException>(() => assignment.SubmitAssignment(student));

        error.Code.Should().Be("Attachment.NotClean");
        error.Message.Should().Contain("dosya");
        work.Status.Should().NotBe(StudentAssignmentStatus.Submitted);

        var (assignment2, work2, student2) = Work();
        File(work2); // upload window still open
        Assert.Throws<BusinessRuleException>(() => assignment2.SubmitAssignment(student2)).Code.Should().Be("Attachment.NotClean");
    }

    [Fact]
    public void GradedWorkTakesNoNewFiles()
    {
        var (assignment, work, student) = Work();
        assignment.SubmitAssignment(student);
        assignment.GradeAssignment(student, 90);

        var error = Assert.Throws<BusinessRuleException>(() => work.AddSubmissionAttachment("key", "a.jpg", "image/jpeg", 10, new string('a', 64)));
        error.Code.Should().Be("Attachment.Locked");
        Assert.Throws<BusinessRuleException>(() => work.EnsureAcceptingAttachments()).Code.Should().Be("Attachment.Locked");
    }

    [Fact]
    public void AResubmissionKeepsTheFirstHandInTimeAndRecordsTheLatestOne()
    {
        var (assignment, work, student) = Work();
        assignment.SubmitAssignment(student, "ilk");
        var first = work.SubmittedAt;
        first.Should().NotBeNull();
        work.LastSubmittedAt.Should().Be(first);

        Thread.Sleep(5);
        assignment.SubmitAssignment(student, "ikinci");

        work.SubmittedAt.Should().Be(first, "an on-time hand-in stays on time when the student resubmits later");
        work.LastSubmittedAt.Should().BeAfter(first!.Value);
        work.StudentNote.Should().Be("ikinci");
    }
}
