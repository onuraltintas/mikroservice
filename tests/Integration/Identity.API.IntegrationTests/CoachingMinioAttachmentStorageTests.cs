using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using DotNet.Testcontainers.Builders;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class CoachingMinioAttachmentStorageTests
{
    [Fact]
    public async Task CoachingErasure_ShouldDeleteTheAttachmentFromMinio()
    {
        const string accessKey = "disposable-test-access";
        const string secretKey = "disposable-test-secret-0123456789";
        await using var container = new ContainerBuilder("quay.io/minio/minio:RELEASE.2025-04-22T22-12-26Z")
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", accessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", secretKey)
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPath("/minio/health/live")
                    .ForPort(9000)))
            .WithCleanUp(true)
            .Build();
        await container.StartAsync();

        var options = Options.Create(new AssignmentAttachmentOptions
        {
            MinioEndpoint = $"{container.Hostname}:{container.GetMappedPublicPort(9000)}",
            MinioAccessKey = accessKey,
            MinioSecretKey = secretKey,
            MinioBucket = $"coaching-e2e-{Guid.NewGuid():N}"
        });
        using var storage = new MinioAssignmentAttachmentStorage(options);
        var key = $"assignments/{Guid.NewGuid():N}/attachment";
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9 };
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await using var content = new MemoryStream(bytes);

        var stored = await storage.StoreAsync(key, content, "image/jpeg", bytes.Length, hash);

        stored.Sha256.Should().Be(hash);
        await using (var read = await storage.OpenReadAsync(key))
        {
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy);
            copy.ToArray().Should().Equal(bytes);
        }

        await using var context = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var subjectId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Disposable E2E", DateTime.UtcNow.AddDays(1));
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
        var action = () => storage.OpenReadAsync(key);
        await action.Should().ThrowAsync<Exception>();
    }
}
