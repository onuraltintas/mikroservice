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
}
