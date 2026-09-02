using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsMediaPolicyTests
{
    [Theory]
    [InlineData("image/jpeg", "photo.jpg", ".jpg")]
    [InlineData("image/jpeg", "photo.jpeg", ".jpeg")]
    [InlineData("image/png", "photo.png", ".png")]
    [InlineData("image/webp", "photo.webp", ".webp")]
    [InlineData("image/gif", "photo.gif", ".gif")]
    public void Accepts_supported_image_types_and_returns_a_safe_extension(
        string contentType,
        string fileName,
        string expectedExtension)
    {
        CmsMediaPolicy.GetValidatedExtension(contentType, fileName, 1024)
            .Should().Be(expectedExtension);
    }

    [Theory]
    [InlineData("image/svg+xml", "image.svg")]
    [InlineData("text/html", "image.html")]
    [InlineData("image/png", "image.jpg")]
    public void Rejects_unsupported_or_mismatched_image_metadata(string contentType, string fileName)
    {
        var action = () => CmsMediaPolicy.GetValidatedExtension(contentType, fileName, 1024);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_files_larger_than_the_cms_limit()
    {
        var action = () => CmsMediaPolicy.GetValidatedExtension(
            "image/png",
            "large.png",
            CmsMediaPolicy.MaxFileSizeBytes + 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("image/png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })]
    [InlineData("image/jpeg", new byte[] { 255, 216, 255, 224 })]
    [InlineData("image/gif", new byte[] { 71, 73, 70, 56 })]
    public void Recognizes_supported_image_signatures(string contentType, byte[] header)
    {
        CmsMediaPolicy.HasValidSignature(contentType, header).Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_file_with_a_spoofed_content_type()
    {
        CmsMediaPolicy.HasValidSignature("image/png", new byte[] { 255, 216, 255, 224 })
            .Should().BeFalse();
    }
}
