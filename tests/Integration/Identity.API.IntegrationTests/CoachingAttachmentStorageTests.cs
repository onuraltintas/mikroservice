using Coaching.Application.Attachments;
using Coaching.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAttachmentStorageTests
{
    [Fact]
    public async Task LocalStorage_StoresJpegOnlyAfterSignatureAndHashValidation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"eduplatform-attachments-{Guid.NewGuid():N}");
        var options = Options.Create(new AssignmentAttachmentOptions { RootPath = root });
        var storage = new LocalAssignmentAttachmentStorage(options);
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9 };
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

        try
        {
            await using var content = new MemoryStream(bytes);
            var stored = await storage.StoreAsync(
                "assignments/test/attachment-1",
                content,
                "image/jpeg",
                bytes.Length,
                hash,
                CancellationToken.None);

            stored.SizeBytes.Should().Be(bytes.Length);
            stored.Sha256.Should().Be(hash);
            var storedPath = Path.Combine(root, "assignments", "test", "attachment-1");
            File.Exists(storedPath).Should().BeTrue();

            await using (var readStream = await storage.OpenReadAsync("assignments/test/attachment-1"))
            {
                using var copy = new MemoryStream();
                await readStream.CopyToAsync(copy);
                copy.ToArray().Should().Equal(bytes);
            }

            await storage.DeleteAsync("assignments/test/attachment-1");
            File.Exists(storedPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DevelopmentScanner_ReturnsCleanForDevelopmentProfile()
    {
        var scanner = new DevelopmentAttachmentScanner();
        await using var content = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });

        var result = await scanner.ScanAsync(content);

        result.IsClean.Should().BeTrue();
        result.ThreatName.Should().BeNull();
    }

    [Fact]
    public async Task LocalStorage_RejectsJpegExtensionWithNonJpegContent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"eduplatform-attachments-{Guid.NewGuid():N}");
        var storage = new LocalAssignmentAttachmentStorage(
            Options.Create(new AssignmentAttachmentOptions { RootPath = root }));

        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("not an image");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            await using var content = new MemoryStream(bytes);

            var act = () => storage.StoreAsync(
                "assignments/test/attachment-2",
                content,
                "image/jpeg",
                bytes.Length,
                hash,
                CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("application/pdf", "%PDF-1.7\n")]
    [InlineData("application/msword", "\u00d0\u00cf\u0011\u00e0\u00a1\u00b1\u001a\u00e1")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK\u0003\u0004\u0014\u0000\u0000\u0000")]
    [InlineData("text/markdown", "# Homework\nAnswer\n")]
    public async Task LocalStorage_StoresSupportedDocumentsAfterSignatureAndHashValidation(
        string contentType,
        string contentText)
    {
        var root = Path.Combine(Path.GetTempPath(), $"eduplatform-attachments-{Guid.NewGuid():N}");
        var storage = new LocalAssignmentAttachmentStorage(
            Options.Create(new AssignmentAttachmentOptions { RootPath = root }));
        var bytes = System.Text.Encoding.Latin1.GetBytes(contentText);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

        try
        {
            await using var content = new MemoryStream(bytes);
            var stored = await storage.StoreAsync(
                "assignments/test/document-1",
                content,
                contentType,
                bytes.Length,
                hash,
                CancellationToken.None);

            stored.ContentType.Should().Be(contentType);
            stored.SizeBytes.Should().Be(bytes.Length);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
