using Coaching.Application.Commands.CreateAssignment;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingBookAssignmentTests
{
    [Fact]
    public void SetBookReference_StoresSourceAndExerciseRange()
    {
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Matematik kitabı",
            DateTime.UtcNow.AddDays(2));

        assignment.SetBookReference(
            bookTitle: "TYT Matematik",
            startPage: 42,
            endPage: 48,
            startQuestion: 1,
            endQuestion: 12,
            isbn: "978-0000000000",
            edition: "2026",
            chapter: "Fonksiyonlar");

        assignment.Source.Should().Be(AssignmentSource.Book);
        assignment.BookTitle.Should().Be("TYT Matematik");
        assignment.BookStartPage.Should().Be(42);
        assignment.BookEndPage.Should().Be(48);
        assignment.BookStartQuestion.Should().Be(1);
        assignment.BookEndQuestion.Should().Be(12);
        assignment.BookChapter.Should().Be("Fonksiyonlar");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 9)]
    public void SetBookReference_RejectsInvalidPageRange(int startPage, int endPage)
    {
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Kitap ödevi",
            DateTime.UtcNow.AddDays(2));

        var act = () => assignment.SetBookReference("Kitap", startPage, endPage);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CreateAssignmentValidator_RequiresBookDetailsForBookSource()
    {
        var validator = new CreateAssignmentCommandValidator();
        var command = new CreateAssignmentCommand
        {
            TeacherId = Guid.NewGuid(),
            Title = "Kitap ödevi",
            AssignmentType = "Individual",
            AssignmentSource = "Book",
            DueDate = DateTime.UtcNow.AddDays(1),
            StudentIds = [Guid.NewGuid()],
            IdempotencyKey = "book-assignment-key-1"
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateAssignmentCommand.BookTitle));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateAssignmentCommand.BookStartPage));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateAssignmentCommand.BookEndPage));
    }

    [Fact]
    public void AssignmentStudent_SubmissionAttachment_RequiresCleanScanBeforeSubmit()
    {
        var assignmentId = Guid.NewGuid();
        var student = AssignmentStudent.Create(assignmentId, Guid.NewGuid());
        var attachment = student.AddSubmissionAttachment(
            storageKey: $"assignments/{Guid.NewGuid():N}/photo-1",
            originalFileName: "ödev.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            sha256: new string('a', 64));
        attachment.SetUploadExpiry(DateTime.UtcNow.AddMinutes(5));

        var submitBeforeScan = () => student.Submit();
        submitBeforeScan.Should().Throw<InvalidOperationException>();

        attachment.MarkUploaded();
        attachment.MarkClean();
        student.Submit();

        attachment.Status.Should().Be(AttachmentScanStatus.Clean);
        student.Status.Should().Be(StudentAssignmentStatus.Submitted);
    }

    [Fact]
    public void AssignmentStudent_ExpiredAttachmentCannotBeUploaded()
    {
        var student = AssignmentStudent.Create(Guid.NewGuid(), Guid.NewGuid());
        var attachment = student.AddSubmissionAttachment(
            storageKey: $"assignments/{Guid.NewGuid():N}/photo-2",
            originalFileName: "ödev.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            sha256: new string('a', 64));
        attachment.SetUploadExpiry(DateTime.UtcNow.AddMinutes(-1));

        attachment.IsUploadWindowOpen(DateTime.UtcNow).Should().BeFalse();
        var act = () => attachment.MarkUploaded();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Attachment upload window has expired.");
    }
}
