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
