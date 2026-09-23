using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLocalAttachmentErasureTests
{
    [Theory]
    [InlineData("../other-user/file")]
    [InlineData("/absolute/file")]
    [InlineData("other\\user/file")]
    public async Task LocalStorage_RejectsUnsafePaths(string key)
    {
        var storage = new LocalAssignmentAttachmentStorage(Options.Create(
            new AssignmentAttachmentOptions { RootPath = Path.GetTempPath() }));

        var action = () => storage.OpenReadAsync(key);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CoachingErasure_DeletesLocalAttachmentAndPreservesUnrelatedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"coaching-erasure-{Guid.NewGuid():N}");
        var storage = new LocalAssignmentAttachmentStorage(Options.Create(
            new AssignmentAttachmentOptions { RootPath = root }));
        var key = $"assignments/{Guid.NewGuid():N}/attachment";
        var otherKey = $"assignments/{Guid.NewGuid():N}/attachment";
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9 };
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        try
        {
            await using (var content = new MemoryStream(bytes))
                await storage.StoreAsync(key, content, "image/jpeg", bytes.Length, hash);
            await using (var content = new MemoryStream(bytes))
                await storage.StoreAsync(otherKey, content, "image/jpeg", bytes.Length, hash);

            await using var context = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var subjectId = Guid.NewGuid();
            var requestId = Guid.NewGuid();
            var assignment = Assignment.Create(Guid.NewGuid(), "Local attachment erasure", DateTime.UtcNow.AddDays(1));
            assignment.AssignToStudent(subjectId);
            assignment.AssignedStudents.Single().AddSubmissionAttachment(
                key, "attachment.jpg", "image/jpeg", bytes.Length, hash);
            context.Assignments.Add(assignment);
            await context.SaveChangesAsync();
            await new CoachingErasureAssessmentService(context, TimeProvider.System).AssessAsync(
                new PersonalDataErasureAssessmentRequestedV1(
                    Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, DryRun: true),
                CancellationToken.None);

            var result = await new CoachingErasureExecutionService(context, storage, TimeProvider.System)
                .ExecuteAsync(new PersonalDataErasureExecutionRequestedV1(
                    Guid.NewGuid(), requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Coaching),
                    CancellationToken.None);

            result.DeletedRecordCount.Should().Be(2);
            (await context.AssignmentSubmissionAttachments.CountAsync()).Should().Be(0);
            var erasedRead = () => storage.OpenReadAsync(key);
            await erasedRead.Should().ThrowAsync<FileNotFoundException>();
            await using var unrelated = await storage.OpenReadAsync(otherKey);
            unrelated.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
