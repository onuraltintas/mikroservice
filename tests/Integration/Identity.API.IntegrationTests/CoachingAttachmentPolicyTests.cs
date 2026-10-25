using Coaching.Application.Attachments;
using Coaching.Application.Commands.CreateAssignmentAttachment;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAttachmentPolicyTests
{
    [Fact]
    public void ValidateMetadata_AllowsSupportedPhoto()
    {
        var act = () => AssignmentAttachmentPolicy.ValidateMetadata(
            "solution-01.jpg",
            "image/jpeg",
            5 * 1024);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("answer.pdf", "application/pdf")]
    [InlineData("answer.doc", "application/msword")]
    [InlineData("answer.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("answer.md", "text/markdown")]
    public void ValidateMetadata_AllowsDocuments(string fileName, string contentType)
    {
        var act = () => AssignmentAttachmentPolicy.ValidateMetadata(fileName, contentType, 1024);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("answer.pdf", "image/jpeg")]
    [InlineData("answer.doc", "application/pdf")]
    [InlineData("answer.docx", "application/msword")]
    [InlineData("answer.md", "application/octet-stream")]
    public void ValidateMetadata_RejectsMismatchedDocumentTypes(string fileName, string contentType)
    {
        var act = () => AssignmentAttachmentPolicy.ValidateMetadata(fileName, contentType, 1024);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("payload.exe", "application/octet-stream", 1024)]
    [InlineData("../../escape.jpg", "image/jpeg", 1024)]
    [InlineData("solution\r\n.jpg", "image/jpeg", 1024)]
    [InlineData("solution.jpg", "image/jpeg", 10_485_761)]
    [InlineData("solution.jpg", "image/png", 0)]
    public void ValidateMetadata_RejectsUnsafeOrOversizedFiles(
        string fileName,
        string contentType,
        long sizeBytes)
    {
        var act = () => AssignmentAttachmentPolicy.ValidateMetadata(fileName, contentType, sizeBytes);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task CreateAttachmentValidator_RequiresSha256AndSupportedPhoto()
    {
        var validator = new CreateAssignmentAttachmentCommandValidator();
        var command = new CreateAssignmentAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "answer.jpg",
            "image/jpeg",
            1024,
            "not-a-sha");

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateAssignmentAttachmentCommand.Sha256));
    }
}
